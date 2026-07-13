namespace PayRunIO.ReportBuilder.Services
{
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Converts raw XML text into HTML where each token is wrapped in a class-based span
    /// (xml-delim, xml-tag, xml-attr, xml-value, xml-comment) so RQL can be rendered colourised.
    /// The palette lives in app.css. Lenient by design: anything that does not scan as markup is
    /// emitted as plain encoded text, so malformed XML degrades gracefully rather than erroring.
    /// </summary>
    public static class XmlSyntaxHighlighter
    {
        private static readonly Regex MarkupRegex = new(
            "(?<comment><!--[\\s\\S]*?-->|<!\\[CDATA\\[[\\s\\S]*?\\]\\]>|<![^>]*>)"
            + "|<(?<open>[/?]?)(?<name>[A-Za-z_][\\w:.-]*)(?<attrs>(?:\"[^\"]*\"|'[^']*'|[^>\"'])*?)(?<close>[/?]?)>",
            RegexOptions.Compiled);

        private static readonly Regex AttributeTokenRegex = new(
            "(?<value>\"[^\"]*\"|'[^']*')|(?<eq>=)|(?<name>[\\w:.-]+)",
            RegexOptions.Compiled);

        public static string ToHtml(string? xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(xml.Length * 2);
            var index = 0;

            foreach (Match match in MarkupRegex.Matches(xml))
            {
                AppendText(builder, xml.Substring(index, match.Index - index));

                if (match.Groups["comment"].Success)
                {
                    AppendSpan(builder, "xml-comment", match.Value);
                }
                else
                {
                    AppendTag(builder, match);
                }

                index = match.Index + match.Length;
            }

            AppendText(builder, xml.Substring(index));

            return builder.ToString();
        }

        private static void AppendTag(StringBuilder builder, Match match)
        {
            AppendSpan(builder, "xml-delim", "<" + match.Groups["open"].Value);
            AppendSpan(builder, "xml-tag", match.Groups["name"].Value);

            var attrs = match.Groups["attrs"].Value;
            var index = 0;

            foreach (Match token in AttributeTokenRegex.Matches(attrs))
            {
                AppendText(builder, attrs.Substring(index, token.Index - index));

                if (token.Groups["value"].Success)
                {
                    AppendSpan(builder, "xml-value", token.Value);
                }
                else if (token.Groups["eq"].Success)
                {
                    AppendSpan(builder, "xml-delim", token.Value);
                }
                else
                {
                    AppendSpan(builder, "xml-attr", token.Value);
                }

                index = token.Index + token.Length;
            }

            AppendText(builder, attrs.Substring(index));
            AppendSpan(builder, "xml-delim", match.Groups["close"].Value + ">");
        }

        private static void AppendSpan(StringBuilder builder, string cssClass, string text)
        {
            builder.Append("<span class=\"").Append(cssClass).Append("\">")
                .Append(WebUtility.HtmlEncode(text))
                .Append("</span>");
        }

        private static void AppendText(StringBuilder builder, string text)
        {
            if (text.Length > 0)
            {
                builder.Append(WebUtility.HtmlEncode(text));
            }
        }
    }
}
