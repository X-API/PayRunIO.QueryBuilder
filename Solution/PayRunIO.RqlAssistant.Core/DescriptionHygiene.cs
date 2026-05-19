namespace PayRunIO.RqlAssistant.Service
{
    using System.Text.RegularExpressions;

    public static class DescriptionHygiene
    {
        private static readonly Regex XmlDocMarkerRegex =
            new(@"^\s*/{2,}\s*|\s*/{2,}\s*$", RegexOptions.Compiled);

        private static readonly Regex WhitespaceRegex =
            new(@"\s+", RegexOptions.Compiled);

        public static string Strip(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return string.Empty;
            }

            var stripped = XmlDocMarkerRegex.Replace(description, string.Empty);

            return WhitespaceRegex.Replace(stripped, " ").Trim();
        }
    }
}
