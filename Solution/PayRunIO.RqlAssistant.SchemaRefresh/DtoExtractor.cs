namespace PayRunIO.RqlAssistant.SchemaRefresh
{
    using System.Reflection;
    using System.Text.Json;
    using System.Xml.Linq;

    using PayRunIO.RqlAssistant.Service;

    internal static class DtoExtractor
    {
        private const string ResourcePrefix = "PayRunIO.v2.Models.Schemas.";
        private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

        // The 10 query/grammar XSDs are emitted by XsdRebuilder, not as DTOs.
        // Common.xsd is a shared primitive-type definition file, not a DTO.
        private static readonly HashSet<string> NonDtoSchemas = new(StringComparer.OrdinalIgnoreCase)
        {
            "Query", "Common",
            "QueryConditionBase", "QueryEntityGroup", "QueryFilterBase",
            "QueryNamespace", "QueryNameValuePair", "QueryOrderByBase",
            "QueryOutputAggregateBase", "QueryOutputBase"
        };

        public static int Run()
        {
            var modelsAssembly = typeof(PayRunIO.v2.Models.Employee).Assembly;
            var resourceDir = Paths.ResolveResourceDirectory();
            var finalPath = Path.Combine(resourceDir, "dtos.json");

            Console.WriteLine($"dtos: enumerating DTOs from PayRunIO.v2.Models {modelsAssembly.GetName().Version}");

            // Index by short name across all PayRunIO.v2.Models* namespaces. Abstract
            // bases (FileBase, ConditionBase) and reporting-subnamespace types
            // (Filtering.EqualTo, Conditions.When) are legitimate DTOs in the schema
            // graph, so include both.
            var clrTypes = modelsAssembly
                .GetExportedTypes()
                .Where(t => t.IsClass
                            && !t.IsGenericTypeDefinition
                            && t.Namespace != null
                            && t.Namespace.StartsWith("PayRunIO.v2.Models", StringComparison.Ordinal))
                .GroupBy(t => t.Name)
                .ToDictionary(g => g.Key, PickPreferredType, StringComparer.Ordinal);

            var schemaNames = modelsAssembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                            && n.EndsWith(".xsd", StringComparison.Ordinal))
                .Select(n => n.Substring(ResourcePrefix.Length, n.Length - ResourcePrefix.Length - 4))
                .Where(n => !NonDtoSchemas.Contains(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Emit a record for every CLR DTO. If it has a matching XSD, use the rich
            // XSD prose; otherwise fall back to a CLR-only record (name + properties,
            // empty descriptions). This matches the original dtos.json scope.
            var classes = clrTypes
                .Values
                .Where(IncludeAsDto)
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => BuildClassRecord(t, modelsAssembly, schemaNames.Contains(t.Name) ? t.Name : null))
                .ToList();

            // Report XSDs that have no matching CLR type (schema-only types or
            // generated wrappers like ArrayOfEmployee).
            var skipped = schemaNames
                .Where(n => !clrTypes.ContainsKey(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int oldCount = -1;
            if (File.Exists(finalPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(finalPath));
                    if (doc.RootElement.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        oldCount = data.GetArrayLength();
                    }
                }
                catch (JsonException)
                {
                }
            }

            var root = new SchemaRoot { Data = classes };
            var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
            Paths.WriteAtomic(finalPath, json);

            var delta = oldCount >= 0 ? $" ({classes.Count - oldCount:+#;-#;0} vs existing)" : string.Empty;
            Console.WriteLine($"dtos: wrote {finalPath}");
            Console.WriteLine($"dtos: {classes.Count} classes{delta}, {classes.Sum(c => c.Properties.Count)} properties, {new FileInfo(finalPath).Length:N0} bytes");

            if (skipped.Count > 0)
            {
                Console.WriteLine($"dtos: note: {skipped.Count} XSD(s) have no matching CLR class (schema-only / wrappers): {string.Join(", ", skipped.Take(6))}{(skipped.Count > 6 ? ", ..." : string.Empty)}");
            }

            return 0;
        }

        // When two types share a short name across namespaces (rare but possible —
        // e.g. an internal duplicate), prefer the one in the root namespace, then the
        // shortest namespace, then ordinal. Deterministic so reruns produce the same output.
        private static Type PickPreferredType(IGrouping<string, Type> group)
        {
            return group
                .OrderBy(t => t.Namespace == "PayRunIO.v2.Models" ? 0 : 1)
                .ThenBy(t => t.Namespace?.Length ?? int.MaxValue)
                .ThenBy(t => t.FullName, StringComparer.Ordinal)
                .First();
        }

        // Filter out CLR types that aren't data-carrying DTOs: attributes, exceptions,
        // delegates (handled by IsClass elsewhere), and obvious infrastructure types.
        private static bool IncludeAsDto(Type type)
        {
            if (type.IsSubclassOf(typeof(Attribute))) return false;
            if (type.IsSubclassOf(typeof(Exception))) return false;
            if (type.IsSubclassOf(typeof(Delegate))) return false;

            // Static classes (abstract + sealed) carrying no instance state — these
            // are helpers/constants (FilterBaseExtensions, MetaDataCommonKeys), not
            // data DTOs.
            if (type.IsAbstract && type.IsSealed) return false;

            return true;
        }

        private static ClassRecord BuildClassRecord(Type clrType, Assembly assembly, string? schemaName)
        {
            // Primary lookup: per-type XSD file matching the class name. Secondary
            // lookup: scan the cached bundle index (built once per run) for the
            // complexType wherever it actually lives (e.g. EqualTo lives in
            // QueryFilterBase.xsd alongside its siblings).
            var (classDoc, propDocs) = schemaName != null
                ? LoadXsdDocs(assembly, schemaName)
                : (string.Empty, new Dictionary<string, string>(StringComparer.Ordinal));

            if (string.IsNullOrEmpty(classDoc) && propDocs.Count == 0)
            {
                if (BundleIndex(assembly).TryGetValue(clrType.Name, out var found))
                {
                    classDoc = found.classDoc;
                    propDocs = found.propDocs;
                }
            }

            var properties = clrType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Select(p => new PropertyRecord
                {
                    Name = p.Name,
                    Type = FormatClrType(p.PropertyType),
                    Description = DescriptionHygiene.Strip(propDocs.GetValueOrDefault(p.Name, string.Empty))
                })
                .ToList();

            return new ClassRecord
            {
                ClassName = clrType.Name,
                Description = DescriptionHygiene.Strip(classDoc),
                Properties = properties
            };
        }

        private static Dictionary<string, (string classDoc, Dictionary<string, string> propDocs)>? _bundleIndex;

        // Walks every embedded .xsd once, finds every <complexType name="X">, and
        // caches an X -> (classDoc, propDocs) map. Lets us recover docs for types
        // bundled inside multi-type schema files (e.g. EqualTo in QueryFilterBase.xsd).
        private static Dictionary<string, (string classDoc, Dictionary<string, string> propDocs)> BundleIndex(Assembly assembly)
        {
            if (_bundleIndex != null)
            {
                return _bundleIndex;
            }

            var index = new Dictionary<string, (string, Dictionary<string, string>)>(StringComparer.Ordinal);

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                    || !resourceName.EndsWith(".xsd", StringComparison.Ordinal))
                {
                    continue;
                }

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) continue;

                XDocument doc;
                try
                {
                    doc = XDocument.Load(stream);
                }
                catch (System.Xml.XmlException)
                {
                    continue;
                }

                foreach (var ct in doc.Descendants(Xsd + "complexType"))
                {
                    var name = (string?)ct.Attribute("name");
                    if (string.IsNullOrEmpty(name) || index.ContainsKey(name!))
                    {
                        continue;
                    }

                    var classDoc = FirstDocumentation(ct);
                    var propDocs = new Dictionary<string, string>(StringComparer.Ordinal);

                    foreach (var el in ct.Descendants(Xsd + "element"))
                    {
                        var pn = (string?)el.Attribute("name");
                        if (string.IsNullOrEmpty(pn) || propDocs.ContainsKey(pn!)) continue;
                        propDocs[pn!] = FirstDocumentation(el);
                    }

                    foreach (var attr in ct.Descendants(Xsd + "attribute"))
                    {
                        var pn = (string?)attr.Attribute("name");
                        if (string.IsNullOrEmpty(pn) || propDocs.ContainsKey(pn!)) continue;
                        propDocs[pn!] = FirstDocumentation(attr);
                    }

                    index[name!] = (classDoc, propDocs);
                }
            }

            _bundleIndex = index;
            return _bundleIndex;
        }

        private static (string classDoc, Dictionary<string, string> propDocs) LoadXsdDocs(Assembly assembly, string schemaName)
        {
            var emptyResult = (string.Empty, new Dictionary<string, string>(StringComparer.Ordinal));

            using var stream = assembly.GetManifestResourceStream(ResourcePrefix + schemaName + ".xsd");
            if (stream == null)
            {
                return emptyResult;
            }

            XDocument doc;
            try
            {
                doc = XDocument.Load(stream);
            }
            catch (System.Xml.XmlException)
            {
                return emptyResult;
            }

            // Find the complexType matching this DTO. Some XSDs define more than one
            // (e.g. nested helper types); we want the one whose name matches the file.
            var complexType = doc.Descendants(Xsd + "complexType")
                .FirstOrDefault(ct => (string?)ct.Attribute("name") == schemaName);

            if (complexType == null)
            {
                return emptyResult;
            }

            var classDoc = FirstDocumentation(complexType);

            var propDocs = new Dictionary<string, string>(StringComparer.Ordinal);

            // Property-bearing positions inside a complexType: <xsd:element name="...">,
            // optionally also <xsd:attribute name="...">. CLR property names match by
            // case (.NET XmlSerializer convention).
            foreach (var element in complexType.Descendants(Xsd + "element"))
            {
                var name = (string?)element.Attribute("name");
                if (string.IsNullOrEmpty(name) || propDocs.ContainsKey(name!))
                {
                    continue;
                }

                propDocs[name!] = FirstDocumentation(element);
            }

            foreach (var attribute in complexType.Descendants(Xsd + "attribute"))
            {
                var name = (string?)attribute.Attribute("name");
                if (string.IsNullOrEmpty(name) || propDocs.ContainsKey(name!))
                {
                    continue;
                }

                propDocs[name!] = FirstDocumentation(attribute);
            }

            return (classDoc, propDocs);
        }

        private static string FirstDocumentation(XElement element)
        {
            // Take the documentation from the direct child <xsd:annotation>, not from
            // any nested simpleType/complexType that may carry its own.
            var annotation = element.Elements(Xsd + "annotation").FirstOrDefault();
            if (annotation == null)
            {
                return string.Empty;
            }

            var documentation = annotation.Elements(Xsd + "documentation").FirstOrDefault();
            if (documentation == null)
            {
                return string.Empty;
            }

            // documentation may carry mixed content (text + html-ish spans). Flattening
            // the inner text preserves the visible prose; hygiene strips noisy markers.
            return string.Concat(documentation.Nodes().Select(NodeToText));
        }

        private static string NodeToText(XNode node) => node switch
        {
            XText t => t.Value,
            XElement e => string.Concat(e.Nodes().Select(NodeToText)),
            _ => string.Empty
        };

        // Render a CLR type using the convention seen in the existing dtos.json:
        //  - Nullable<T>            => "T?"
        //  - IEnumerable<T>/List<T> => "Collection<T>"
        //  - primitives             => C# alias ("string", "int", "bool", ...)
        //  - everything else        => short type name
        private static string FormatClrType(Type type)
        {
            var nullableUnderlying = Nullable.GetUnderlyingType(type);
            if (nullableUnderlying != null)
            {
                return FormatClrType(nullableUnderlying) + "?";
            }

            if (type.IsGenericType)
            {
                var generic = type.GetGenericTypeDefinition();
                if (IsCollectionGeneric(generic))
                {
                    var arg = type.GetGenericArguments()[0];
                    return "Collection<" + FormatClrType(arg) + ">";
                }
            }

            if (type.IsArray)
            {
                var elem = type.GetElementType();
                return "Collection<" + (elem != null ? FormatClrType(elem) : "object") + ">";
            }

            return PrimitiveAlias(type) ?? type.Name;
        }

        private static bool IsCollectionGeneric(Type generic)
        {
            if (generic == typeof(List<>)) return true;
            if (generic == typeof(IList<>)) return true;
            if (generic == typeof(ICollection<>)) return true;
            if (generic == typeof(IEnumerable<>)) return true;
            if (generic == typeof(IReadOnlyList<>)) return true;
            if (generic == typeof(IReadOnlyCollection<>)) return true;
            // System.Collections.ObjectModel.Collection<T> & friends:
            if (generic.Name.StartsWith("Collection`", StringComparison.Ordinal)) return true;
            return false;
        }

        private static string? PrimitiveAlias(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(short)) return "short";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";
            if (type == typeof(char)) return "char";
            if (type == typeof(object)) return "object";
            return null;
        }

        private sealed class SchemaRoot
        {
            public List<ClassRecord> Data { get; init; } = new();
        }

        private sealed class ClassRecord
        {
            public string ClassName { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public List<PropertyRecord> Properties { get; init; } = new();
        }

        private sealed class PropertyRecord
        {
            public string Name { get; init; } = string.Empty;
            public string Type { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
        }
    }
}
