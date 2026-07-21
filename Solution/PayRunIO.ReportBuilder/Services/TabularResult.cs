namespace PayRunIO.ReportBuilder.Services
{
    using System.Xml.Linq;

    /// <summary>
    /// Parsed representation of a tabular RQL response: root node "Table" containing a "Headers"
    /// group of column names, a "Rows" group of "Row" items, and an optional trailing "Footer"
    /// row (typically column totals) rendered emphasised beneath the data rows.
    /// </summary>
    public sealed class TabularResult
    {
        public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();

        public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<IReadOnlyList<string>>();

        /// <summary>
        /// The optional footer row, or null when the query renders no footer. When present it holds
        /// one value per column in the same order as <see cref="Columns"/>.
        /// </summary>
        public IReadOnlyList<string>? Footer { get; init; }

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

            // An optional "Footer" group (typically column totals) renders as a single element
            // directly under the root, holding one 'col' per column. It carries no "Row" items, so
            // the row scan above never picks it up; capture it separately for emphasised rendering.
            var footerElement = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Footer");

            var footer = footerElement?.Elements().Select(e => e.Value).ToList();

            return new TabularResult { Columns = columns, Rows = rows, Footer = footer };
        }
    }
}
