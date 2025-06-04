namespace PayRunIO.QueryBuilder
{
    using System;
    using System.IO;
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

            Action<string> callBack = textResult =>
                {
                    this.QueryResponseDocument = new TextDocument(textResult);

                    if (connection.ContentType == ContentType.XML)
                    {
                        this.foldingManager = FoldingManager.Install(this.ResultViewTextEditor.TextArea);
                        var foldingStrategy = new XmlFoldingStrategy();
                        foldingStrategy.UpdateFoldings(this.foldingManager, this.ResultViewTextEditor.Document);
                    }
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

        private static async Task<string> GetResponseText(IRestApiHelperAsync restApiHelper, Query query, ContentType responseType)
        {
            string textResult = null;

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

            return textResult;
        }

        private static Task<string> GetQueryResult(string query, Func<string, string, Task<string>> queryMethod)
        {
            var result = queryMethod("/Query", query);

            return result;
        }
        //
        // private void DeleteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        // {
        //     e.CanExecute = ApiProfiles.Instance.Profiles.Count > 1;
        // }
        //
        // private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        // {
        //     var profileToDelete = this.SelectedProfile;
        //
        //     var nextProfile = ApiProfiles.Instance.Profiles.FirstOrDefault(p => p != profileToDelete);
        //
        //     if (nextProfile != null)
        //     {
        //         this.SelectedProfile = nextProfile;
        //         ApiProfiles.Instance.DeleteProfile(profileToDelete.Name);
        //         this.Expander.IsExpanded = false;
        //     }
        // }
        //
        // private void NewCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        // {
        //     e.CanExecute = true;
        // }
        //
        // private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        // {
        //     int count = 1;
        //
        //     var name = $"Profile ({count:000})";
        //
        //     while (ApiProfiles.Instance[name] != null)
        //     {
        //         name = $"Profile ({++count:000})";
        //     }
        //
        //     ApiProfiles.Instance.AddProfile(name);
        //
        //     var apiProfile = ApiProfiles.Instance[name];
        //
        //     apiProfile.ApiHostUrl = "https://api.test.payrun.io";
        //     apiProfile.ResponseType = "XML";
        //
        //     this.SelectedProfile = apiProfile;
        //
        //     this.Expander.IsExpanded = true;
        // }
    }
}
