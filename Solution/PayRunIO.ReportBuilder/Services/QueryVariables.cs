namespace PayRunIO.ReportBuilder.Services
{
    using System.Xml;
    using System.Xml.Linq;

    public sealed record QueryVariable(string Name, string Value);

    /// <summary>
    /// Reads and rewrites the root-level &lt;Variables&gt; block of an RQL query so report
    /// parameters (employer key, dates, etc.) can be edited without touching the XML by hand.
    /// </summary>
    public static class QueryVariables
    {
        public static IReadOnlyList<QueryVariable> Parse(string? queryXml)
        {
            if (string.IsNullOrWhiteSpace(queryXml))
            {
                return Array.Empty<QueryVariable>();
            }

            try
            {
                var document = XDocument.Parse(queryXml);

                return FindVariableElements(document)
                    .Select(e => new QueryVariable(
                        e.Attribute("Name")?.Value ?? string.Empty,
                        e.Attribute("Value")?.Value ?? string.Empty))
                    .Where(v => !string.IsNullOrEmpty(v.Name))
                    .ToList();
            }
            catch (XmlException)
            {
                return Array.Empty<QueryVariable>();
            }
        }

        public static string SetValue(string queryXml, string name, string value)
        {
            try
            {
                var document = XDocument.Parse(queryXml, LoadOptions.PreserveWhitespace);

                var variable = FindVariableElements(document)
                    .FirstOrDefault(e => e.Attribute("Name")?.Value == name);

                if (variable == null)
                {
                    return queryXml;
                }

                variable.SetAttributeValue("Value", value);

                return document.ToString(SaveOptions.DisableFormatting);
            }
            catch (XmlException)
            {
                return queryXml;
            }
        }

        private static IEnumerable<XElement> FindVariableElements(XDocument document) =>
            document.Root?
                .Elements()
                .Where(e => e.Name.LocalName == "Variables")
                .SelectMany(e => e.Elements())
                .Where(e => e.Name.LocalName == "Variable")
            ?? Enumerable.Empty<XElement>();
    }
}
