namespace PayRunIO.QueryBuilder
{
    using System;
    using System.IO;
    using System.Windows;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using PayRunIO.ConnectionControls.Updates;
    using PayRunIO.QueryBuilder.Configuration;
    using PayRunIO.QueryBuilder.Services;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// The identifier used to locate this application within the developer portal update manifest.
        /// </summary>
        public const string ApplicationId = "query-builder";

        private IHost? host;

        /// <summary>
        /// Gets the service provider for dependency injection.
        /// </summary>
        public IServiceProvider ServiceProvider => this.host?.Services ?? throw new InvalidOperationException("Host not initialized");

        /// <summary>
        /// Gets the directory holding appsettings.json.
        /// </summary>
        /// <remarks>
        /// The application publishes as a self contained single file, and appsettings.json is
        /// deliberately kept outside the bundle so that it remains editable after installation.
        /// AppDomain.CurrentDomain.BaseDirectory cannot be used to find it: for a bundled
        /// application that resolves to the temporary extraction directory
        /// (%TEMP%\.net\PayRunIO.QueryBuilder\&lt;hash&gt;), not the install folder. Environment.ProcessPath
        /// is the location of the executable itself, which is where the file actually sits.
        /// </remarks>
        /// <returns>The directory containing the application executable.</returns>
        private static string GetSettingsDirectory()
        {
            var processPath = Environment.ProcessPath;

            return string.IsNullOrEmpty(processPath)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(processPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// Application startup event handler.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(GetSettingsDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Create host with dependency injection
            this.host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register configuration
                    services.AddSingleton<IConfiguration>(configuration);

                    // Register settings service
                    services.AddSingleton<ISettingsService, SettingsService>();
                })
                .Build();

            // Create and show the main window manually
            var mainWindow = PayRunIO.QueryBuilder.MainWindow.Create();
            mainWindow.Show();

            // Best effort background check against the developer portal. Never blocks startup.
            UpdateCheckStarter.StartBackgroundCheck(ApplicationId, mainWindow);

            base.OnStartup(e);
        }

        /// <summary>
        /// Application exit event handler.
        /// </summary>
        /// <param name="e">The exit event arguments.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            // Save settings before exit
            var settingsService = this.ServiceProvider?.GetService<ISettingsService>();
            settingsService?.SaveUserSettings();

            this.host?.Dispose();
            base.OnExit(e);
        }

        /// <summary>
        /// The application dispatcher unhandled exception method.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The dispatcher unhandled exception event args.</param>
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"An unhandled exception just occurred:{Environment.NewLine}{Environment.NewLine}{e.Exception}", 
                $"Unhandled Exception - {e.Exception.GetType().Name}", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);

            e.Handled = true;
        }
    }
}
