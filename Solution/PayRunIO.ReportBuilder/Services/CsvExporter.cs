namespace PayRunIO.ReportBuilder.Services
{
    using System.Text;

    public static class CsvExporter
    {
        public static string ToCsv(TabularResult table)
        {
            var builder = new StringBuilder();

            AppendRow(builder, table.Columns);

            foreach (var row in table.Rows)
            {
                AppendRow(builder, row);
            }

            if (table.Footer != null)
            {
                AppendRow(builder, table.Footer);
            }

            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, IReadOnlyList<string> values)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(Escape(values[i]));
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
