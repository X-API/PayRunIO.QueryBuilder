namespace PayRunIO.QueryBuilder.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    using System.Windows.Media;

    using Markdown.Xaml;

    using PayRunIO.RqlAssistant.Service.Models;

    internal class ChatMessageToFlowDocumentValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessage chatMessage)
            {
                var markdown = new Markdown();

                var text = $"*{chatMessage.Role}:*\r\n\r\n{chatMessage.Text}";

                // Transform markdown to FlowDocument
                var doc = markdown.Transform(text);

                // Apply styles
                doc.FontFamily = new FontFamily("Segoe UI");
                doc.FontSize = 14;

                return doc;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
