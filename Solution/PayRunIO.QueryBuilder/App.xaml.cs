namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Windows;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using PayRunIO.QueryBuilder.Configuration;
    using PayRunIO.QueryBuilder.Services;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private IHost? host;

        /// <summary>
        /// Gets the service provider for dependency injection.
        /// </summary>
        public IServiceProvider ServiceProvider => this.host?.Services ?? throw new InvalidOperationException("Host not initialized");

        /// <summary>
        /// Application startup event handler.
        /// </summary>
        /// <param name="e">The startup event arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
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

            // Get settings service
            var settingsService = this.ServiceProvider.GetRequiredService<ISettingsService>();

            // Create and show the main window manually
            var mainWindow = PayRunIO.QueryBuilder.MainWindow.Create();
            mainWindow.Show();

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
