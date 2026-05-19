namespace PayRunIO.RqlAssistant.SchemaRefresh
{
    using System.Text.Json;

    using PayRunIO.RqlAssistant.Service;

    internal static class RoutesConverter
    {
        public static int Run(string? csvPath)
        {
            csvPath ??= Paths.DefaultRoutesCsv();
            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException($"Routes CSV not found at {csvPath}");
            }

            Console.WriteLine($"routes: reading {csvPath}");

            var rows = ReadCsv(csvPath);
            var routes = rows.Select(MapRow).ToList();

            var resourceDir = Paths.ResolveResourceDirectory();
            var finalPath = Path.Combine(resourceDir, "routes.json");

            int oldCount = -1;
            if (File.Exists(finalPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(finalPath));
                    oldCount = doc.RootElement.GetArrayLength();
                }
                catch (JsonException)
                {
                    // Existing file is unreadable; treat as missing for the delta report.
                }
            }

            var json = JsonSerializer.Serialize(routes, new JsonSerializerOptions { WriteIndented = true });
            Paths.WriteAtomic(finalPath, json);

            var delta = oldCount >= 0 ? $" ({routes.Count - oldCount:+#;-#;0} vs existing)" : string.Empty;
            Console.WriteLine($"routes: wrote {finalPath}");
            Console.WriteLine($"routes: {routes.Count} routes{delta}, {new FileInfo(finalPath).Length:N0} bytes");
            return 0;
        }

        private static RouteRecord MapRow(IReadOnlyDictionary<string, string> row)
        {
            var opId = row.GetValueOrDefault("OperationId", string.Empty);
            var tagsRaw = row.GetValueOrDefault("Tags", string.Empty);
            var tags = string.IsNullOrWhiteSpace(tagsRaw)
                ? Array.Empty<string>()
                : tagsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries);

            int.TryParse(row.GetValueOrDefault("ResponseCode", "0"), out var responseCode);

            return new RouteRecord
            {
                ClassName = opId + "Route",
                Route = "RouteTemplate",
                RouteSignature = row.GetValueOrDefault("Route", string.Empty),
                OperationId = opId,
                Verb = row.GetValueOrDefault("Verb", string.Empty).ToUpperInvariant(),
                Summary = DescriptionHygiene.Strip(row.GetValueOrDefault("Summary")),
                Description = DescriptionHygiene.Strip(row.GetValueOrDefault("Description")),
                Tags = tags,
                ResponseCode = responseCode,
                ResponseType = row.GetValueOrDefault("Response", string.Empty)
            };
        }

        // Minimal RFC 4180-ish CSV reader: handles double-quoted fields with embedded
        // commas, newlines and "" escapes. Sufficient for the Routes.csv generator and
        // avoids a new package dependency.
        private static List<Dictionary<string, string>> ReadCsv(string path)
        {
            var text = File.ReadAllText(path);
            var fields = ParseCsv(text);

            if (fields.Count == 0)
            {
                return new List<Dictionary<string, string>>();
            }

            var header = fields[0];
            var result = new List<Dictionary<string, string>>(fields.Count - 1);

            for (var i = 1; i < fields.Count; i++)
            {
                var row = fields[i];
                if (row.Count == 1 && string.IsNullOrEmpty(row[0]))
                {
                    continue;
                }

                var dict = new Dictionary<string, string>(header.Count, StringComparer.OrdinalIgnoreCase);
                for (var j = 0; j < header.Count; j++)
                {
                    dict[header[j]] = j < row.Count ? row[j] : string.Empty;
                }

                result.Add(dict);
            }

            return result;
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var current = new List<string>();
            var field = new System.Text.StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    switch (c)
                    {
                        case '"':
                            inQuotes = true;
                            break;
                        case ',':
                            current.Add(field.ToString());
                            field.Clear();
                            break;
                        case '\r':
                            break;
                        case '\n':
                            current.Add(field.ToString());
                            field.Clear();
                            rows.Add(current);
                            current = new List<string>();
                            break;
                        default:
                            field.Append(c);
                            break;
                    }
                }
            }

            if (field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                rows.Add(current);
            }

            return rows;
        }

        private sealed class RouteRecord
        {
            public string ClassName { get; init; } = string.Empty;
            public string Route { get; init; } = string.Empty;
            public string RouteSignature { get; init; } = string.Empty;
            public string OperationId { get; init; } = string.Empty;
            public string Verb { get; init; } = string.Empty;
            public string Summary { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
            public int ResponseCode { get; init; }
            public string ResponseType { get; init; } = string.Empty;
        }
    }
}
