namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Windows;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
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
