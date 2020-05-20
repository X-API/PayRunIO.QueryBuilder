namespace PayRunIO.QueryBuilder
{
    using System;
    using System.IO;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Forms.VisualStyles;
    using System.Windows.Input;
    using System.Xml;

    using ICSharpCode.AvalonEdit.Document;
    using ICSharpCode.AvalonEdit.Folding;

    using PayRunIO.CSharp.SDK;
    using PayRunIO.Models.Reporting;
    using PayRunIO.OAuth1;

    /// <summary>
    /// Interaction logic for QueryResultViewer.xaml
    /// </summary>
    public partial class QueryResultViewer : UserControl
    {
        private readonly OAuthSignatureGenerator oAuthSignatureGenerator;

        public QueryResultViewer()
        {
            this.InitializeComponent();
            this.oAuthSignatureGenerator = new OAuthSignatureGenerator();
        }

        public static readonly DependencyProperty ApiHostUrlProperty = DependencyProperty.Register("ApiHostUrl", typeof(string), typeof(QueryResultViewer), new PropertyMetadata(AppSettings.Default.ApiHostUrl, OnSettingChanged));

        public static readonly DependencyProperty ConsumerKeyProperty = DependencyProperty.Register("ConsumerKey", typeof(string), typeof(QueryResultViewer), new PropertyMetadata(AppSettings.Default.ConsumerKey, OnSettingChanged));

        public static readonly DependencyProperty ConsumerSecretProperty = DependencyProperty.Register("ConsumerSecret", typeof(string), typeof(QueryResultViewer), new PropertyMetadata(AppSettings.Default.ConsumerSecret, OnSettingChanged));

        public static readonly DependencyProperty ResponseTypeProperty = DependencyProperty.Register("ResponseType", typeof(string), typeof(QueryResultViewer), new PropertyMetadata(AppSettings.Default.ResponseType, OnSettingChanged));

        public static readonly DependencyProperty QueryProperty = DependencyProperty.Register("Query", typeof(Query), typeof(QueryResultViewer), new PropertyMetadata(default(Query)));

        public static readonly DependencyProperty QueryResponseDocumentProperty = DependencyProperty.Register("QueryResponseDocument", typeof(TextDocument), typeof(QueryResultViewer), new PropertyMetadata(default(TextDocument)));

        private FoldingManager foldingManager;

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

        public string ResponseType
        {
            get => (string)this.GetValue(ResponseTypeProperty);
            set => this.SetValue(ResponseTypeProperty, value);
        }

        public string ConsumerSecret
        {
            get => (string)this.GetValue(ConsumerSecretProperty);
            set => this.SetValue(ConsumerSecretProperty, value);
        }

        public string ConsumerKey
        {
            get => (string)this.GetValue(ConsumerKeyProperty);
            set => this.SetValue(ConsumerKeyProperty, value);
        }

        public string ApiHostUrl
        {
            get => (string)this.GetValue(ApiHostUrlProperty);
            set => this.SetValue(ApiHostUrlProperty, value);
        }

        public string[] ResponseTypes { get; } = { "XML", "JSON" };

        private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            AppSettings.Default[e.Property.Name] = e.NewValue;
            AppSettings.Default.Save();
        }

        private void RefreshCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = 
                !string.IsNullOrEmpty(this.ConsumerKey)
                && !string.IsNullOrEmpty(this.ConsumerSecret)
                && !string.IsNullOrEmpty(this.ApiHostUrl);
        }

        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var contentTypeHeader = AppSettings.Default.ResponseType == "XML" ? "application/xml" : "application/json";

            var restApiHelper = 
                new RestApiHelper(
                    this.oAuthSignatureGenerator, 
                    AppSettings.Default.ConsumerKey, 
                    AppSettings.Default.ConsumerSecret, 
                    AppSettings.Default.ApiHostUrl, 
                    contentTypeHeader, 
                    contentTypeHeader);

            try
            {
                string textResult;
                if (AppSettings.Default.ResponseType == "XML")
                {
                    var queryAsXml = XmlSerialiserHelper.SerialiseToXmlDoc(this.Query).InnerXml;

                    var rawResult = restApiHelper.PostRawXml("/Query", queryAsXml);

                    var xmlDoc = new XmlDocument { PreserveWhitespace = true };

                    xmlDoc.LoadXml(rawResult);

                    textResult = xmlDoc.Beautify();
                }
                else
                {
                    string queryAsJson;
                    using (var jsonStream = JsonSerialiserHelper.Serialise(this.Query))
                    {
                        using (var sr = new StreamReader(jsonStream))
                        {
                            queryAsJson = sr.ReadToEnd();
                        }
                    }

                    textResult = restApiHelper.PostRawJson("/Query", queryAsJson);
                }

                if (this.foldingManager != null)
                {
                    FoldingManager.Uninstall(this.foldingManager);
                    this.foldingManager = null;
                }

                this.QueryResponseDocument = new TextDocument(textResult);

                if (AppSettings.Default.ResponseType == "XML")
                {
                    this.foldingManager = FoldingManager.Install(this.ResultViewTextEditor.TextArea);
                    var foldingStrategy = new XmlFoldingStrategy();
                    foldingStrategy.UpdateFoldings(this.foldingManager, this.ResultViewTextEditor.Document);
                }
            }
            catch (Exception ex)
            {
                var result = new StringBuilder();

                result.AppendLine("An error occured while executing the query");
                result.AppendLine(string.Empty);
                result.AppendLine(ex.ToString());

                this.QueryResponseDocument = new TextDocument(result.ToString());
            }
        }
    }
}
