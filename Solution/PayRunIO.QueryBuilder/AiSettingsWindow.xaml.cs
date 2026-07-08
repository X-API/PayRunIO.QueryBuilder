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

            this.ApiKeyBox.Password = this.settingsService.UserSettings.OpenAI.ApiKey ?? string.Empty;
            this.EndPointBox.Text = this.settingsService.UserSettings.OpenAI.Endpoint ?? string.Empty;
            this.ModelBox.Text = this.settingsService.UserSettings.OpenAI.Model ?? string.Empty;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            this.settingsService.UserSettings.OpenAI.ApiKey = this.ApiKeyBox.Password;
            this.settingsService.UserSettings.OpenAI.Endpoint = this.EndPointBox.Text;
            this.settingsService.UserSettings.OpenAI.Model = this.ModelBox.Text;
            this.settingsService.SaveUserSettings();

            this.StatusText.Text = $"Saved at {DateTime.Now:T}";
            this.StatusText.Visibility = Visibility.Visible;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
