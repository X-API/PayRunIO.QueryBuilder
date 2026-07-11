namespace PayRunIO.ReportBuilder.Services
{
    using System.Xml.Linq;

    /// <summary>
    /// Parsed representation of a tabular RQL response: root node "Table" containing a "Headers"
    /// group of column names followed by a "Rows" group of "Row" items.
    /// </summary>
    public sealed class TabularResult
    {
        public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();

        public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<IReadOnlyList<string>>();

        /// <summary>
        /// Returns null when the document does not follow the tabular output pattern; callers then
        /// fall back to the raw XML view.
        /// </summary>
        public static TabularResult? TryParse(XDocument document)
        {
            var root = document.Root;

            if (root == null)
            {
                return null;
            }

            var headers = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Headers");

            if (headers == null)
            {
                return null;
            }

            var columns = headers.Elements().Select(e => e.Value).ToList();

            if (columns.Count == 0)
            {
                return null;
            }

            var rows = new List<IReadOnlyList<string>>();

            // Collect every "Row" item wherever it sits. A flat tabular query places the "Rows" group
            // directly under the root; grouped queries (e.g. one section per schedule) can nest it as
            // Table > Schedules > Schedule > Rows > Row. Descending for "Row" flattens both into the
            // same table — every Row still carries the full column set, so nothing is lost. The linter
            // warns against the nested shape, but the reader tolerates it rather than exporting nothing.
            foreach (var row in root.Descendants().Where(e => e.Name.LocalName == "Row"))
            {
                rows.Add(row.Elements().Select(e => e.Value).ToList());
            }

            return new TabularResult { Columns = columns, Rows = rows };
        }
    }
}
