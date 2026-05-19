namespace PayRunIO.RqlAssistant.SchemaRefresh
{
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Schema;

    internal static class XsdRebuilder
    {
        // Order matters: types defined in earlier files are referenced by later files.
        // The original hand-assembled XSD followed this order; preserving it keeps
        // dependency resolution working and minimises diff churn.
        private static readonly string[] OrderedSchemaNames =
        {
            "Query",
            "Common",
            "QueryConditionBase",
            "QueryEntityGroup",
            "QueryFilterBase",
            "QueryNamespace",
            "QueryNameValuePair",
            "QueryOrderByBase",
            "QueryOutputAggregateBase",
            "QueryOutputBase"
        };

        private const string ResourcePrefix = "PayRunIO.v2.Models.Schemas.";

        public static int Run(bool force)
        {
            var modelsAssembly = LoadModelsAssembly();
            var packageVersion = modelsAssembly.GetName().Version?.ToString(4) ?? "0.0.0.0";

            var resourceDir = Paths.ResolveResourceDirectory();
            var finalPath = Path.Combine(resourceDir, "QuerySchema.xsd");

            Console.WriteLine($"xsd: assembling QuerySchema.xsd from PayRunIO.v2.Models {packageVersion}");

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<xsd:schema version=\"{packageVersion}\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
            sb.AppendLine();

            foreach (var name in OrderedSchemaNames)
            {
                var inner = ExtractInner(modelsAssembly, name);
                sb.AppendLine($"    <!-- ===== {name}.xsd ===== -->");
                sb.AppendLine(inner);
                sb.AppendLine();
            }

            sb.AppendLine("</xsd:schema>");

            var content = sb.ToString();
            var tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var (typeCount, elementCount) = ValidateCompiles(tempPath);

            int oldTypeCount = -1;
            if (File.Exists(finalPath))
            {
                oldTypeCount = CountComplexTypes(File.ReadAllText(finalPath));
                var newTypeCount = CountComplexTypes(content);
                if (newTypeCount < oldTypeCount && !force)
                {
                    File.Delete(tempPath);
                    throw new InvalidOperationException(
                        $"Refusing to write: new XSD has {newTypeCount} complexTypes vs existing {oldTypeCount}. "
                        + "Pass --force to override.");
                }
            }

            // Compile-checked content already on disk at tempPath; finalise the swap
            // through the shared atomic-write helper (handles the Windows lock case).
            Paths.WriteAtomic(finalPath, content);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            var oldNote = oldTypeCount >= 0 ? $", was {oldTypeCount}" : string.Empty;
            Console.WriteLine($"xsd: wrote {finalPath}");
            Console.WriteLine($"xsd: {content.Length:N0} bytes, {typeCount} global types ({CountComplexTypes(content)} complexTypes{oldNote}), {elementCount} global elements");
            return 0;
        }

        private static Assembly LoadModelsAssembly()
        {
            // Touch a known DTO type so the package's assembly + side-loaded dependencies
            // are loaded the normal way (avoids the LoadFile resolver gap we hit when
            // probing the NuGet cache directly).
            var anchor = typeof(PayRunIO.v2.Models.Employee);
            return anchor.Assembly;
        }

        private static string ExtractInner(Assembly assembly, string name)
        {
            var resourceName = ResourcePrefix + name + ".xsd";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            var openEnd = content.IndexOf('>', content.IndexOf("<xsd:schema", StringComparison.Ordinal));
            var closeStart = content.LastIndexOf("</xsd:schema>", StringComparison.Ordinal);
            if (openEnd < 0 || closeStart < 0)
            {
                throw new InvalidOperationException($"Could not locate <xsd:schema> wrapper in {resourceName}");
            }

            var inner = content.Substring(openEnd + 1, closeStart - openEnd - 1);

            // Strip both forms of <xsd:include>: self-closing and block-with-annotation.
            // Block form first, otherwise the self-closing pattern leaves a dangling
            // </xsd:include>.
            inner = Regex.Replace(inner, @"<xsd:include\b[^>]*?>.*?</xsd:include>", string.Empty, RegexOptions.Singleline);
            inner = Regex.Replace(inner, @"<xsd:include\b[^>]*?/>", string.Empty);

            return inner.Trim('\r', '\n', ' ', '\t');
        }

        private static (int typeCount, int elementCount) ValidateCompiles(string path)
        {
            var set = new XmlSchemaSet();
            set.Add(targetNamespace: null, schemaUri: path);
            set.Compile();
            return (set.GlobalTypes.Count, set.GlobalElements.Count);
        }

        private static int CountComplexTypes(string xsd) =>
            Regex.Matches(xsd, @"<xsd:complexType\s+name=""").Count;
    }
}
