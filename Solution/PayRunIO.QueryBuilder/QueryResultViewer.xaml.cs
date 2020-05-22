namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
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

        private FoldingManager foldingManager;

        public QueryResultViewer()
        {
            this.InitializeComponent();
            this.oAuthSignatureGenerator = new OAuthSignatureGenerator();
        }

        public static readonly DependencyProperty ApiHostUrlProperty = DependencyProperty.Register("ApiHostUrl", typeof(string), typeof(QueryResultViewer), new PropertyMetadata(ApiProfiles.Instance.SelectedProfile.ApiHostUrl, OnSettingChanged));

        public static readonly DependencyProperty ConsumerKeyProperty = DependencyProperty.Register("ConsumerKey", typeof(string), typeof(QueryResultViewer), new PropertyMetadata(ApiProfiles.Instance.SelectedProfile.ConsumerKey, OnSettingChanged));

        public static readonly DependencyProperty ConsumerSecretProperty = DependencyProperty.Register("ConsumerSecret", typeof(string), typeof(QueryResultViewer), new PropertyMetadata(ApiProfiles.Instance.SelectedProfile.ConsumerSecret, OnSettingChanged));

        public static readonly DependencyProperty ResponseTypeProperty = DependencyProperty.Register("ResponseType", typeof(string), typeof(QueryResultViewer), new PropertyMetadata(ApiProfiles.Instance.SelectedProfile.ResponseType, OnSettingChanged));

        public static readonly DependencyProperty QueryProperty = DependencyProperty.Register("Query", typeof(Query), typeof(QueryResultViewer), new PropertyMetadata(default(Query)));

        public static readonly DependencyProperty QueryResponseDocumentProperty = DependencyProperty.Register("QueryResponseDocument", typeof(TextDocument), typeof(QueryResultViewer), new PropertyMetadata(default(TextDocument)));

        public static readonly DependencyProperty ProfilesProperty = DependencyProperty.Register("ApiProfiles", typeof(ObservableCollection<ApiProfile>), typeof(QueryResultViewer), new PropertyMetadata(ApiProfiles.Instance.Profiles));

        public static readonly DependencyProperty SelectedProfileProperty = DependencyProperty.Register("SelectedProfile", typeof(ApiProfile), typeof(QueryResultViewer), new PropertyMetadata(ApiProfiles.Instance.SelectedProfile, OnSelectedProfileChanged));

        public ApiProfile SelectedProfile
        {
            get => (ApiProfile)this.GetValue(SelectedProfileProperty);
            set => this.SetValue(SelectedProfileProperty, value);
        }

        public ObservableCollection<ApiProfile> Profiles
        {
            get => (ObservableCollection<ApiProfile>)this.GetValue(ProfilesProperty);
            set => this.SetValue(ProfilesProperty, value);
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

        private static void OnSelectedProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ApiProfiles.Instance.SelectedProfile = (ApiProfile)e.NewValue;
        }

        private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            switch (e.Property.Name)
            {
                case nameof(ApiProfile.Name):
                    ApiProfiles.Instance.SelectedProfile.Name = (string)e.NewValue;
                    break;
                case nameof(ApiProfile.ApiHostUrl):
                    ApiProfiles.Instance.SelectedProfile.ApiHostUrl = (string)e.NewValue;
                    break;
                case nameof(ApiProfile.ConsumerKey):
                    ApiProfiles.Instance.SelectedProfile.ConsumerKey = (string)e.NewValue;
                    break;
                case nameof(ApiProfile.ConsumerSecret):
                    ApiProfiles.Instance.SelectedProfile.ConsumerSecret = (string)e.NewValue;
                    break;
                case nameof(ApiProfile.ResponseType):
                    ApiProfiles.Instance.SelectedProfile.ResponseType = (string)e.NewValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(e.Property.Name, $"Api Profile Property {e.Property.Name} not supported");
            }

            ApiProfiles.Instance.Save();
        }

        private void RefreshCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.SelectedProfile != null
                && Uri.TryCreate(this.SelectedProfile.ApiHostUrl, UriKind.Absolute, out var uri)
                && !string.IsNullOrEmpty(this.SelectedProfile.ConsumerKey)
                && !string.IsNullOrEmpty(this.SelectedProfile.ConsumerSecret);
        }

        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.SelectedProfile == null)
            {
                return;
            }

            var contentTypeHeader = this.SelectedProfile.ResponseType == "XML" ? "application/xml" : "application/json";

            var restApiHelper = 
                new RestApiHelper(
                    this.oAuthSignatureGenerator,
                    this.SelectedProfile.ConsumerKey,
                    this.SelectedProfile.ConsumerSecret,
                    this.SelectedProfile.ApiHostUrl, 
                    contentTypeHeader, 
                    contentTypeHeader);

            try
            {
                string textResult;
                if (this.SelectedProfile.ResponseType == "XML")
                {
                    var queryAsXml = XmlSerialiserHelper.SerialiseToXmlDoc(this.Query).InnerXml;

                    var rawResult = GetQueryResult(queryAsXml, restApiHelper.PostRawXml);

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

                    textResult = GetQueryResult(queryAsJson, restApiHelper.PostRawXml);
                }

                if (this.foldingManager != null)
                {
                    FoldingManager.Uninstall(this.foldingManager);
                    this.foldingManager = null;
                }

                this.QueryResponseDocument = new TextDocument(textResult);

                if (this.SelectedProfile.ResponseType == "XML")
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

                if (this.foldingManager != null)
                {
                    FoldingManager.Uninstall(this.foldingManager);
                    this.foldingManager = null;
                }

                this.QueryResponseDocument = new TextDocument(result.ToString());
            }
        }

        private static string GetQueryResult(string query, Func<string, string, string> queryMethod)
        {
            var result = queryMethod("/Query", query);

            return result;
        }

        private void DeleteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ApiProfiles.Instance.Profiles.Count > 1;
        }

        private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var profileToDelete = this.SelectedProfile;

            var nextProfile = ApiProfiles.Instance.Profiles.FirstOrDefault(p => p != profileToDelete);

            if (nextProfile != null)
            {
                this.SelectedProfile = nextProfile;
                ApiProfiles.Instance.DeleteProfile(profileToDelete.Name);
                this.Expander.IsExpanded = false;
            }
        }

        private void NewCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int count = 1;

            var name = $"Profile ({count:000})";

            while (ApiProfiles.Instance[name] != null)
            {
                name = $"Profile ({++count:000})";
            }

            ApiProfiles.Instance.AddProfile(name);

            var apiProfile = ApiProfiles.Instance[name];

            apiProfile.ApiHostUrl = "https://api.test.payrun.io";
            apiProfile.ResponseType = "XML";

            this.SelectedProfile = apiProfile;

            this.Expander.IsExpanded = true;
        }
    }
}
