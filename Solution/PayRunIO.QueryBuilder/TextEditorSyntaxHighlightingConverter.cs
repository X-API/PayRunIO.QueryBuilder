namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    using ICSharpCode.AvalonEdit.Highlighting;

    public class TextEditorSyntaxHighlightingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var valueAsString = value as string;

            if (string.IsNullOrEmpty(valueAsString))
            {
                return "XML";
            }

            var definitionName = 
                valueAsString.Equals("XML", StringComparison.InvariantCultureIgnoreCase)
                    ? "XML"
                    : "JavaScript";

            return HighlightingManager.Instance.GetDefinition(definitionName);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
