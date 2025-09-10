namespace PayRunIO.QueryBuilder
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;

    using Microsoft.Extensions.Configuration;

    using PayRunIO.QueryBuilder.Services;
    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Models;
    using PayRunIO.v2.CSharp.SDK;
    using PayRunIO.v2.Models.Reporting;

    /// <summary>
    /// Interaction logic for AiAssistantWindow.xaml
    /// </summary>
    public partial class AiAssistantWindow : Window, INotifyPropertyChanged
    {
        private readonly ISettingsService settingsService;

        private IRqlRagService rqlRagService;

        private string initialQueryAsXml;

        private bool isBusy;

        public static readonly DependencyProperty QueryProperty =
            DependencyProperty.Register(
                nameof(Query),
                typeof(Query),
                typeof(AiAssistantWindow),
                new PropertyMetadata(default(Query)));
        
        public static readonly DependencyProperty IncludeSchemasAndRoutesProperty = 
            DependencyProperty.Register(
                nameof(IncludeSchemasAndRoutes), 
                typeof(bool), 
                typeof(AiAssistantWindow), 
                new PropertyMetadata(true));

        public static readonly DependencyProperty TabularQueryProperty = DependencyProperty.Register(nameof(TabularQuery), typeof(bool), typeof(AiAssistantWindow), new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty AutoProcessQuestionProperty = 
            DependencyProperty.Register(
                nameof(AutoProcessQuestion), 
                typeof(bool), 
                typeof(AiAssistantWindow), 
                new PropertyMetadata(false));

        public bool TabularQuery
        {
            get => (bool)GetValue(TabularQueryProperty);
            set => this.SetValue(TabularQueryProperty, value);
        }

        public bool IncludeSchemasAndRoutes
        {
            get => (bool)GetValue(IncludeSchemasAndRoutesProperty);
            set => this.SetValue(IncludeSchemasAndRoutesProperty, value);
        }

        public bool AutoProcessQuestion
        {
            get => (bool)GetValue(AutoProcessQuestionProperty);
            set => this.SetValue(AutoProcessQuestionProperty, value);
        }

        public Query Query
        {
            get => (Query)GetValue(QueryProperty);
            set
            {
                this.SetValue(QueryProperty, value);

                if (this.initialQueryAsXml == null)
                {
                    this.initialQueryAsXml = value?.ToXml() ?? string.Empty;
                }
            }
        }

        public bool IsBusy
        {
            get => this.isBusy;
            set
            {
                this.isBusy = value;
                this.OnPropertyChanged();
            }
        }

        public AiAssistantWindow(ISettingsService settingsService)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.InitializeComponent();

            // Load user settings into UI controls
            ApiKeyBox.Password = this.settingsService.UserSettings.OpenAI.ApiKey ?? string.Empty;
            EndPointBox.Text = this.settingsService.UserSettings.OpenAI.Endpoint ?? string.Empty;
            ModelBox.Text = this.settingsService.UserSettings.OpenAI.Model ?? string.Empty;

            // Create the services with current settings
            this.CreateRqlRagService();

            // Clear any existing chat history
            this.ChatHistoryControl.MessagesSource.Clear();

            // Add loaded event handler
            this.Loaded += this.OnWindowLoaded;
        }

        private void CreateRqlRagService()
        {
            // Use user settings for OpenAI configuration
            var userSettings = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new[]
                        {
                            new System.Collections.Generic.KeyValuePair<string, string>("OpenAI:ApiKey", this.settingsService.UserSettings.OpenAI.ApiKey ?? string.Empty),
                            new System.Collections.Generic.KeyValuePair<string, string>("OpenAI:Endpoint", this.settingsService.UserSettings.OpenAI.Endpoint ?? string.Empty),
                            new System.Collections.Generic.KeyValuePair<string, string>("OpenAI:Model", this.settingsService.UserSettings.OpenAI.Model ?? string.Empty),
                            new System.Collections.Generic.KeyValuePair<string, string>("OpenAI:Temperature", this.settingsService.UserSettings.OpenAI.Temperature ?? string.Empty)
                        })
                .Build();

            // Create the services
            this.rqlRagService = ServiceFactory.CreateService(userSettings);
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Only auto-process if AutoProcessQuestion is true and there's text in the question box
            if (this.AutoProcessQuestion && !string.IsNullOrWhiteSpace(this.QuestionBox.Text))
            {
                // Ensure we're on the UI thread for UI operations
                await this.Dispatcher.InvokeAsync(async () =>
                {
                    await this.OnAskClick(sender, e);
                });
            }
        }

        private async Task OnAskClick(object sender, RoutedEventArgs e)
        {
            var question = this.QuestionBox.Text.Trim();

            if (string.IsNullOrEmpty(question))
            {
                return;
            }

            var queryAsXml = this.Query?.ToXml() ?? string.Empty;

            this.IsBusy = true;

            this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.User, Text = question });

            question += "\r\n\r\n" + queryAsXml;

            this.QuestionBox.Text = string.Empty;

            string response;
            try
            {
                response = 
                    await
                        this.rqlRagService
                            .AskQuestion(
                                question,
                                includeSchemasAndRoutes: this.IncludeSchemasAndRoutes,
                                chatHistory: this.ChatHistoryControl.MessagesSource,
                                format: this.TabularQuery ? ResponseType.TabularQuery : ResponseType.Conversation);
            }
            catch (OpenAiException exception)
            {
                this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.System, Text = $"[{exception.GetType().Name}] - {exception.StatusCode} - {exception.Message}" });
                this.IsBusy = false;
                return;
            }

            // Find RQL within response:
            var xmlSections = Regex.Matches(response, "```xml\\s*([\\s\\S]*?)\\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match match in xmlSections)
            {
                var innerCode = match.Groups[1].Value;

                if (innerCode.Contains("<Query", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        var sourceXml = SetUtf8(innerCode);

                        this.Query = XmlSerialiserHelper.Deserialise<Query>(sourceXml);

                        response = response.Replace(match.Value, string.Empty);
                    }
                    catch (InvalidOperationException ex)
                    {
                        MessageBox.Show(
                            this,
                            "The assistants response could not be parsed into a valid query.\r\n\r\n" + ex.Message,
                            "Invalid Query Response",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);
                    }
                }
            }

            // Record the chat history for additional questions
            this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = response });

            this.IsBusy = false;
        }

        private static string SetUtf8(string xml) => xml.Replace(" encoding=\"utf-16\"", " encoding=\"utf-8\"");

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            if (this.Query != null)
            {
                var owner = (MainWindow)this.Owner;

                owner.UpdateQuery(this.Query);
            }

            this.Close();
        }

        private bool HasQueryChanges()
        {
            var currentQueryAsXml = this.Query?.ToXml() ?? string.Empty;
            return this.initialQueryAsXml != currentQueryAsXml;
        }

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.HasQueryChanges();
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.Query != null)
            {
                var owner = (MainWindow)this.Owner;

                owner.UpdateQuery(this.Query);
            }

            this.Close();
        }

        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.HasQueryChanges())
            {
                var msgBox = MessageBox.Show("The query has been updated. Do you want to discard the changes?", "Query Changes Detected", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (msgBox == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.Close();
        }

        private void AskAiQueryCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !string.IsNullOrWhiteSpace(this.QuestionBox.Text);
        }

        private async void AskAiQueryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            await this.OnAskClick(sender, e);
        }

        private void OnSaveSettingsClick(object sender, RoutedEventArgs e)
        {
            // Save settings from UI controls
            this.settingsService.UserSettings.OpenAI.ApiKey = ApiKeyBox.Password;
            this.settingsService.UserSettings.OpenAI.Endpoint = EndPointBox.Text;
            this.settingsService.UserSettings.OpenAI.Model = ModelBox.Text;
            this.settingsService.SaveUserSettings();
            
            // Recreate the service with new settings
            this.CreateRqlRagService();
        }
    }
}
