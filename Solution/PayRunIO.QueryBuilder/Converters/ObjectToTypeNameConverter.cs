namespace PayRunIO.QueryBuilder.Converters
{
    using System;
    using System.Text.RegularExpressions;
    using System.Windows.Data;

    [ValueConversion(typeof(object), typeof(string))]
    public class ObjectToTypeNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var typeName = value?.GetType().Name;

            typeName = Regex.Replace(typeName, "(\\B[A-Z])", " $1");

            return typeName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new InvalidOperationException();
        }
    }
}
