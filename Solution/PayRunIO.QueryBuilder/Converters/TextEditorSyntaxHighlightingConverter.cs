namespace PayRunIO.QueryBuilder.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    using ICSharpCode.AvalonEdit.Highlighting;

    using PayRunIO.ConnectionControls.Models;

    public class TextEditorSyntaxHighlightingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string definitionName;

            if (value == null)
            {
                definitionName = "XML";
            }
            else
            {
                definitionName = (ContentType)value == ContentType.XML ? "XML" : "JavaScript";
            }

            return HighlightingManager.Instance.GetDefinition(definitionName);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
