namespace PayRunIO.QueryBuilder
{
    using System;
    using System.ComponentModel;
    using System.Linq;
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
        /// <summary>
        /// Maximum validator-driven retries when the model's RQL output fails XSD validation. Each retry
        /// appends the diagnostics to the conversation as a synthetic user turn so the model can self-correct.
        /// </summary>
        private const int MaxValidationRetries = 2;

        private readonly ISettingsService settingsService;

        private IRqlRagService rqlRagService;

        private IQueryValidator queryValidator;

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

        private AiSettingsWindow settingsWindow;

        public AiAssistantWindow(ISettingsService settingsService)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.InitializeComponent();

            // Create the services with current settings
            this.CreateRqlRagService();

            // Clear any existing chat history
            this.ChatHistoryControl.MessagesSource.Clear();

            // Rebuild the RQL service immediately whenever settings are saved (from this window's
            // Settings button or from MainWindow's AI Settings menu) — no close/reopen needed.
            this.settingsService.SettingsChanged += this.OnSettingsChanged;

            // Add loaded event handler
            this.Loaded += this.OnWindowLoaded;
            this.Closed += this.OnWindowClosed;
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(this.CreateRqlRagService);
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            this.settingsService.SettingsChanged -= this.OnSettingsChanged;
            this.settingsWindow?.Close();
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            if (this.settingsWindow == null)
            {
                this.settingsWindow = new AiSettingsWindow(this.settingsService) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                this.settingsWindow.Closed += (s, args) => this.settingsWindow = null;
            }

            this.settingsWindow.ShowDialog();
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
                            new System.Collections.Generic.KeyValuePair<string, string>("OpenAI:Temperature", this.settingsService.UserSettings.OpenAI.Temperature ?? string.Empty),
                            new System.Collections.Generic.KeyValuePair<string, string>("OpenAI:Provider", this.settingsService.UserSettings.OpenAI.Provider ?? "OpenAI")
                        })
                .Build();

            // Create the services
            this.rqlRagService = ServiceFactory.CreateService(userSettings);
            this.queryValidator = ServiceFactory.CreateValidator();
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

            // Snapshot the history before adding the new question: AskQuestion appends the prompt
            // as the final user turn itself, so including it in the history would show the model
            // the question twice. MessagesSource stays display-only from here on.
            var modelHistory = this.ChatHistoryControl.MessagesSource.ToList();

            this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.User, Text = question });

            var prompt = question + "\r\n\r\n" + queryAsXml;

            this.QuestionBox.Text = string.Empty;

            try
            {
                for (var attempt = 0; attempt <= MaxValidationRetries; attempt++)
                {
                    string response;
                    try
                    {
                        response =
                            await this.rqlRagService.AskQuestion(
                                prompt,
                                includeSchemasAndRoutes: this.IncludeSchemasAndRoutes,
                                chatHistory: modelHistory,
                                format: this.TabularQuery ? ResponseType.TabularQuery : ResponseType.Conversation);
                    }
                    catch (OpenAiException exception)
                    {
                        this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.System, Text = $"[{exception.GetType().Name}] - {exception.StatusCode} - {exception.Message}" });
                        return;
                    }

                    var validationFeedback = this.TryApplyResponse(response, isFinalAttempt: attempt == MaxValidationRetries);

                    if (validationFeedback == null)
                    {
                        // Final reply applied (or no <Query> XML was present) — finish.
                        return;
                    }

                    // Validation failed and retries remain. Move the asked prompt and failed reply into
                    // the model-facing history, surface both in the chat display, and re-ask with the
                    // diagnostics as the new prompt so the model can self-correct.
                    modelHistory.Add(new ChatMessage { Role = ParticipantType.User, Text = prompt });
                    modelHistory.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = response });

                    this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = response });
                    this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.User, Text = validationFeedback });

                    prompt = validationFeedback;
                }
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        /// <summary>
        /// Extracts the first <c>&lt;Query&gt;</c> XML fenced block, validates it against the RQL XSD, and on
        /// success applies it to <see cref="Query"/> and posts the trimmed reply to chat. Returns <c>null</c>
        /// when the reply was successfully applied (or contained no query XML to apply); returns a synthetic
        /// retry prompt with diagnostics when validation failed and retries should continue. On the final
        /// attempt, falls back to the legacy MessageBox + raw reply rather than blocking the chat history.
        /// </summary>
        private string? TryApplyResponse(string response, bool isFinalAttempt)
        {
            var xmlSections = Regex.Matches(response, "```xml\\s*([\\s\\S]*?)\\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            Match? queryMatch = null;
            string? queryXml = null;

            foreach (Match match in xmlSections)
            {
                var innerCode = match.Groups[1].Value;
                if (innerCode.Contains("<Query", StringComparison.InvariantCultureIgnoreCase))
                {
                    queryMatch = match;
                    queryXml = SetUtf8(innerCode);
                    break;
                }
            }

            if (queryMatch == null || queryXml == null)
            {
                // No <Query> XML in the reply — nothing to validate. Treat as a final answer.
                this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = response });
                return null;
            }

            var validation = this.queryValidator.Validate(queryXml);

            if (validation.IsValid)
            {
                try
                {
                    this.Query = XmlSerialiserHelper.Deserialise<Query>(queryXml);
                    var trimmedResponse = response.Replace(queryMatch.Value, string.Empty);
                    this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = trimmedResponse });
                    return null;
                }
                catch (InvalidOperationException ex)
                {
                    if (isFinalAttempt)
                    {
                        MessageBox.Show(
                            this,
                            "The assistants response could not be deserialised into a valid query.\r\n\r\n" + ex.Message,
                            "Invalid Query Response",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);

                        this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = response });
                        return null;
                    }

                    return "The previous query passed XSD validation but failed to deserialise: "
                           + ex.Message
                           + "\r\n\r\nPlease produce a corrected <Query> XML. Call validate_query before finalising.";
                }
            }

            if (isFinalAttempt)
            {
                var diagnosticSummary = string.Join("\r\n", validation.Diagnostics.Select(d =>
                    $"[{d.Severity}] line {d.Line}, col {d.Column}: {d.Code} — {d.Message}"));

                MessageBox.Show(
                    this,
                    "The assistants response failed schema validation after retries.\r\n\r\n" + diagnosticSummary,
                    "Invalid Query Response",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);

                this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = response });
                return null;
            }

            return BuildRetryPrompt(validation);
        }

        private static string BuildRetryPrompt(ValidationResult validation)
        {
            var diagnostics = string.Join("\r\n", validation.Diagnostics.Select(d =>
                $"  [{d.Severity}] line {d.Line}, col {d.Column}: {d.Code} — {d.Message}"));

            return "The <Query> XML in your previous reply failed validation against QuerySchema.xsd:\r\n"
                   + diagnostics
                   + "\r\n\r\nProduce a corrected <Query> XML. Use get_schema / get_route / get_rql_syntax to confirm the right shape, and call validate_query before finalising.";
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

    }
}
