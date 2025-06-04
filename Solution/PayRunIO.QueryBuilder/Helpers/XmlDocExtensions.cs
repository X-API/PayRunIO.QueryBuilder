namespace PayRunIO.QueryBuilder.Helpers
{
    using System.Text;
    using System.Xml;

    public static class XmlDocExtensions
    {
        /// <summary>
        /// Beautify the XML document.
        /// </summary>
        /// <param name="doc">The doc.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string Beautify(this XmlDocument doc)
        {
            var sb = new StringBuilder();
            var settings =
                new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\r\n",
                    NewLineHandling = NewLineHandling.Replace
                };

            var docClone = new XmlDocument();
            docClone.LoadXml(doc.OuterXml);

            using (var writer = XmlWriter.Create(sb, settings))
            {
                docClone.Save(writer);
            }

            return sb.ToString();
        }
    }
}
