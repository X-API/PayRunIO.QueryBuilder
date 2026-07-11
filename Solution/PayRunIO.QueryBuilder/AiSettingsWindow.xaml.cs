namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Windows;

    using PayRunIO.QueryBuilder.Services;

    /// <summary>
    /// Interaction logic for AiSettingsWindow.xaml. Modeless window for configuring the remote
    /// LLM connection; saving persists via <see cref="ISettingsService"/> and raises
    /// <see cref="ISettingsService.SettingsChanged"/> so any open <see cref="AiAssistantWindow"/>
    /// picks up the change immediately without needing to be closed and reopened.
    /// </summary>
    public partial class AiSettingsWindow : Window
    {
        private readonly ISettingsService settingsService;

        public AiSettingsWindow(ISettingsService settingsService)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.InitializeComponent();

            this.ProviderComboBox.Items.Add("OpenAI");
            this.ProviderComboBox.Items.Add("OpenAI (Responses)");
            this.ProviderComboBox.Items.Add("Anthropic");

            var provider = this.settingsService.UserSettings.OpenAI.Provider;
            this.ProviderComboBox.SelectedItem = this.ProviderComboBox.Items.Contains(provider) ? provider : "OpenAI";

            this.ReasoningEffortComboBox.Items.Add(string.Empty);
            this.ReasoningEffortComboBox.Items.Add("none");
            this.ReasoningEffortComboBox.Items.Add("minimal");
            this.ReasoningEffortComboBox.Items.Add("low");
            this.ReasoningEffortComboBox.Items.Add("medium");
            this.ReasoningEffortComboBox.Items.Add("high");

            var reasoningEffort = this.settingsService.UserSettings.OpenAI.ReasoningEffort ?? string.Empty;
            this.ReasoningEffortComboBox.SelectedItem =
                this.ReasoningEffortComboBox.Items.Contains(reasoningEffort) ? reasoningEffort : string.Empty;

            this.ApiKeyBox.Password = this.settingsService.UserSettings.OpenAI.ApiKey ?? string.Empty;
            this.EndPointBox.Text = this.settingsService.UserSettings.OpenAI.Endpoint ?? string.Empty;
            this.ModelBox.Text = this.settingsService.UserSettings.OpenAI.Model ?? string.Empty;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string apiKey = this.ApiKeyBox.Password;
            string endpoint = this.EndPointBox.Text;
            string model = this.ModelBox.Text;
            string provider = this.ProviderComboBox.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(model))
            {
                this.StatusText.Foreground = System.Windows.Media.Brushes.Red;
                this.StatusText.Text = "API Key, Host and Model are all required.";
                this.StatusText.Visibility = Visibility.Visible;
                return;
            }

            this.settingsService.UserSettings.OpenAI.ApiKey = apiKey;
            this.settingsService.UserSettings.OpenAI.Endpoint = endpoint;
            this.settingsService.UserSettings.OpenAI.Model = model;
            this.settingsService.UserSettings.OpenAI.Provider = provider ?? "OpenAI";
            this.settingsService.UserSettings.OpenAI.ReasoningEffort = this.ReasoningEffortComboBox.SelectedItem as string ?? string.Empty;
            this.settingsService.SaveUserSettings();

            this.StatusText.Foreground = System.Windows.Media.Brushes.Green;
            this.StatusText.Text = $"Saved at {DateTime.Now:T}";
            this.StatusText.Visibility = Visibility.Visible;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
