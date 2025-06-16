namespace PayRunIO.QueryBuilder
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Xml;

    using ICSharpCode.AvalonEdit.Document;
    using ICSharpCode.AvalonEdit.Folding;

    using PayRunIO.ConnectionControls;
    using PayRunIO.ConnectionControls.Models;
    using PayRunIO.QueryBuilder.Helpers;
    using PayRunIO.v2.CSharp.SDK;
    using PayRunIO.v2.Models;
    using PayRunIO.v2.Models.Reporting;
    using PayRunIO.v2.OAuth1;

    /// <summary>
    /// Interaction logic for QueryResultViewer.xaml
    /// </summary>
    public partial class QueryResultViewer : UserControl
    {
        private readonly OAuthSignatureGenerator oAuthSignatureGenerator;

        private FoldingManager foldingManager;

        public QueryResultViewer()
        {
            this.InitializeComponent();
            this.oAuthSignatureGenerator = new OAuthSignatureGenerator();
        }

        public static readonly DependencyProperty QueryProperty = DependencyProperty.Register("Query", typeof(Query), typeof(QueryResultViewer), new PropertyMetadata(default(Query)));

        public static readonly DependencyProperty QueryResponseDocumentProperty = DependencyProperty.Register("QueryResponseDocument", typeof(TextDocument), typeof(QueryResultViewer), new PropertyMetadata(default(TextDocument)));

        public static readonly DependencyProperty LastErrorModelProperty = DependencyProperty.Register(nameof(LastErrorModel), typeof(ErrorModel), typeof(QueryResultViewer), new PropertyMetadata(default(ErrorModel)));

        public ErrorModel LastErrorModel
        {
            get => (ErrorModel)GetValue(LastErrorModelProperty);
            set => this.SetValue(LastErrorModelProperty, value);
        }

        public TextDocument QueryResponseDocument
        {
            get => (TextDocument)this.GetValue(QueryResponseDocumentProperty);
            set => this.SetValue(QueryResponseDocumentProperty, value);
        }

        public Query Query
        {
            get => (Query)this.GetValue(QueryProperty);
            set => this.SetValue(QueryProperty, value);
        }

        private void RefreshCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Query != null && this.ConnectionPicker.SelectedConnection != null;
        }

        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.ConnectionPicker.SelectedConnection == null)
            {
                return;
            }

            var connection = this.ConnectionPicker.SelectedConnection;

            var restApiHelper = this.BuildRestApiHelper(connection);

            if (this.foldingManager != null)
            {
                FoldingManager.Uninstall(this.foldingManager);
                this.foldingManager = null;
            }

            this.QueryResponseDocument = new TextDocument("Loading...");

            var query = this.Query;

            Action<(string text, ErrorModel error)> callBack = args =>
                {
                    this.QueryResponseDocument = new TextDocument(args.text);

                    if (connection.ContentType == ContentType.XML)
                    {
                        this.foldingManager = FoldingManager.Install(this.ResultViewTextEditor.TextArea);
                        var foldingStrategy = new XmlFoldingStrategy();
                        foldingStrategy.UpdateFoldings(this.foldingManager, this.ResultViewTextEditor.Document);
                    }

                    this.LastErrorModel = args.error;
                };

            Task
                .Run(async () => await GetResponseText(restApiHelper, query, connection.ContentType))
                .ContinueWith(
                    t =>
                        {
                            this.Dispatcher.BeginInvoke(callBack, t.Result);
                        });
        }

        private IRestApiHelperAsync BuildRestApiHelper(Connection connection)
        {
            var contentHeader = connection.ContentType == ContentType.XML ? "application/xml" : "application/json";

            IRestApiHelperAsync restApiHelper;

            if (connection is Oauth1Connection oauth1Connection)
            {
                restApiHelper = 
                    new AuditRestApiHelper(
                        this.oAuthSignatureGenerator,
                        consumerKey: oauth1Connection.ConsumerKey,
                        consumerSecret: oauth1Connection.ConsumerSecret,
                        hostEndpoint: oauth1Connection.EndPoint,
                        contentTypeHeader: contentHeader,
                        acceptHeader: contentHeader);
            }
            else if (connection is BearerTokenConnection bearerTokenConnection)
            {
                restApiHelper =
                    new BearerTokenRestApiHelper(
                        bearerToken: bearerTokenConnection.BearerToken,
                        hostEndpoint: bearerTokenConnection.EndPoint,
                        contentTypeHeader: contentHeader,
                        acceptHeader: contentHeader);
            }
            else
            {
                throw new NotSupportedException($"Connection type '{connection.GetType().Name}' not currently supported");
            }

            return restApiHelper;
        }

        private static async Task<(string, ErrorModel)> GetResponseText(IRestApiHelperAsync restApiHelper, Query query, ContentType responseType)
        {
            string textResult = null;

            ErrorModel error = null;

            try
            {
                if (responseType == ContentType.XML)
                {
                    var rawResult = await GetQueryResult(query.ToXml(), restApiHelper.PostRawXmlAsync);

                    var xmlDoc = new XmlDocument { PreserveWhitespace = true };

                    xmlDoc.LoadXml(rawResult);

                    textResult = xmlDoc.Beautify();
                }
                else
                {
                    textResult = await GetQueryResult(query.ToJson(), restApiHelper.PostRawJsonAsync);
                }
            }
            catch (ApiResponseException ex)
            {
                error = ex.ErrorModel;

                if (responseType == ContentType.XML)
                {
                    textResult = XmlSerialiserHelper.SerialiseToXmlDoc(ex.ErrorModel).Beautify();
                }
                else
                {
                    using (var jsonStream = JsonSerialiserHelper.Serialise(ex.ErrorModel))
                    {
                        using (var sr = new StreamReader(jsonStream))
                        {
                            textResult = await sr.ReadToEndAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var result = new StringBuilder();

                result.AppendLine("An error occurred while executing the query");
                result.AppendLine(string.Empty);
                result.AppendLine(ex.ToString());

                textResult = result.ToString();
            }

            return (textResult, error);
        }

        private static Task<string> GetQueryResult(string query, Func<string, string, Task<string>> queryMethod)
        {
            var result = queryMethod("/Query", query);

            return result;
        }
    }
}
