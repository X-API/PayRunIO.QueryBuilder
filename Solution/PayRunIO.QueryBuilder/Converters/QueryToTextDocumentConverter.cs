namespace PayRunIO.QueryBuilder.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    using ICSharpCode.AvalonEdit.Document;

    using PayRunIO.ConnectionControls.Models;
    using PayRunIO.v2.CSharp.SDK;
    using PayRunIO.v2.Models.Reporting;

    internal class QueryToTextDocumentConverter : IValueConverter
    {
        public ContentType ContentType { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var query = value as Query;

            if (query == null)
            {
                return new TextDocument(string.Empty);
            }

            return new TextDocument(ContentType == ContentType.XML ? query.ToXml() : query.ToJson());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
