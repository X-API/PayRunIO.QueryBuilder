namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
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

        public string[] ResponseTypes { get; } = { "XML", "JSON" };

        private static void OnSelectedProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ApiProfiles.Instance.SelectedProfile = (ApiProfile)e.NewValue;
            ApiProfiles.Instance.Save();
        }

        private void RefreshCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Query != null 
                && this.SelectedProfile != null
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

            if (this.foldingManager != null)
            {
                FoldingManager.Uninstall(this.foldingManager);
                this.foldingManager = null;
            }

            this.QueryResponseDocument = new TextDocument("Loading...");

            var responseType = this.SelectedProfile.ResponseType;
            var query = this.Query;

            Action<string> callBack = textResult =>
                {
                    this.QueryResponseDocument = new TextDocument(textResult);

                    if (this.SelectedProfile.ResponseType == "XML")
                    {
                        this.foldingManager = FoldingManager.Install(this.ResultViewTextEditor.TextArea);
                        var foldingStrategy = new XmlFoldingStrategy();
                        foldingStrategy.UpdateFoldings(this.foldingManager, this.ResultViewTextEditor.Document);
                    }
                };

            Task<string>.Factory
                .StartNew(() => GetResponseText(restApiHelper, query, responseType))
                .ContinueWith(
                    t =>
                        {
                            this.Dispatcher.BeginInvoke(callBack, t.Result);
                        });
        }

        private static string GetResponseText(RestApiHelper restApiHelper, Query query, string responseType)
        {
            string textResult = null;

            try
            {
                if (responseType == "XML")
                {
                    var queryAsXml = XmlSerialiserHelper.SerialiseToXmlDoc(query).InnerXml;

                    var rawResult = GetQueryResult(queryAsXml, restApiHelper.PostRawXml);

                    var xmlDoc = new XmlDocument { PreserveWhitespace = true };

                    xmlDoc.LoadXml(rawResult);

                    textResult = xmlDoc.Beautify();
                }
                else
                {
                    string queryAsJson;
                    using (var jsonStream = JsonSerialiserHelper.Serialise(query))
                    {
                        using (var sr = new StreamReader(jsonStream))
                        {
                            queryAsJson = sr.ReadToEnd();
                        }
                    }

                    textResult = GetQueryResult(queryAsJson, restApiHelper.PostRawJson);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse response)
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        if (responseStream == null)
                        {
                            throw;
                        }

                        using (var sr = new StreamReader(responseStream))
                        {
                            textResult = sr.ReadToEnd();
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
