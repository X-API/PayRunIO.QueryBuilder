namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Windows;

    using PayRunIO.ConnectionControls;
    using PayRunIO.QueryBuilder.Services;
    using PayRunIO.v2.CSharp.SDK;

    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AboutWindow"/> class.
        /// </summary>
        /// <param name="settingsService">The settings service, used to locate the user settings file.</param>
        public AboutWindow(ISettingsService settingsService)
        {
            ArgumentNullException.ThrowIfNull(settingsService);

            this.Version = this.GetType().Assembly.GetName().Version?.ToString() ?? "?????";
            this.SdkVersion = typeof(HttpRestApiHelper).Assembly.GetName().Version?.ToString() ?? "?????";
            this.UserSettingsPath = settingsService.UserSettingsPath;

            this.InitializeComponent();
        }

        public string Version { get; }

        public string SdkVersion { get; }

        public string ConnectionFilePath { get; } = ConnectionCollectionHelper.ResourceFile.FullName;

        public string UserSettingsPath { get; }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
