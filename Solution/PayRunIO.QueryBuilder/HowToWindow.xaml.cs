namespace PayRunIO.QueryBuilder
{
    using System.Windows;

    public partial class HowToWindow : Window
    {
        public HowToWindow()
        {
            this.InitializeComponent();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
