namespace PayRunIO.RqlAssistant.Service
{
    using System.Collections.ObjectModel;
    using System.Text.Json;

    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// The DocumentRepository interface.
    /// </summary>
    public interface IDocumentRepository
    {
        /// <summary>
        /// The find schema snippets method. Retrieves the schemas related to the specified names.
        /// </summary>
        /// <param name="schemaNames">The schema names.</param>
        /// <returns>
        /// The <see cref="string"/> enumeration.
        /// </returns>
        IEnumerable<string> FindSchemaSnippets(IEnumerable<string> schemaNames);

        /// <summary>
        /// Gets the structured schema definition for a single type by name (exact match, case-insensitive).
        /// </summary>
        /// <param name="typeName">The schema type name.</param>
        /// <returns>The matching <see cref="ClassDefinition"/>, or <c>null</c> if no schema exists with that name.</returns>
        ClassDefinition? GetSchema(string typeName);

        /// <summary>
        /// Lists all known schema definitions, optionally filtered by a case-insensitive name substring.
        /// </summary>
        /// <param name="filter">Optional case-insensitive substring filter applied to <see cref="ClassDefinition.ClassName"/>.</param>
        /// <returns>The matching <see cref="ClassDefinition"/> enumeration.</returns>
        IEnumerable<ClassDefinition> ListSchemas(string? filter = null);

        /// <summary>
        /// Gets all the known route definitions.
        /// </summary>
        /// <returns>
        /// The <see cref="RouteDefinition"/> enumeration.
        /// </returns>
        IEnumerable<RouteDefinition> GetRouteDefinitions();
    }

    /// <summary>
    /// The document repository.
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        /// <summary>
        /// The sync lock.
        /// </summary>
        private readonly object syncLock = new object();

        /// <summary>
        /// The route definitions.
        /// </summary>
        private Collection<RouteDefinition>? routeDefinitions = null;

        /// <summary>
        /// The schema root.
        /// </summary>
        private SchemaRoot? schemaRoot = null;

        /// <summary>
        /// The find schema snippets method. Retrieves the schemas related to the specified names.
        /// </summary>
        /// <param name="schemaNames">The schema names.</param>
        /// <returns>
        /// The <see cref="string"/> enumeration.
        /// </returns>
        public IEnumerable<string> FindSchemaSnippets(IEnumerable<string> schemaNames)
        {
            this.EnsureSchemaRootLoaded();

            if (this.schemaRoot == null)
            {
                return Array.Empty<string>();
            }

            var filteredList = this.schemaRoot.Data.Where(d => schemaNames.Contains(d.ClassName));

            return filteredList.Select(schema => schema.ToString());
        }

        /// <inheritdoc />
        public ClassDefinition? GetSchema(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            this.EnsureSchemaRootLoaded();

            return this.schemaRoot?.Data
                .FirstOrDefault(d => string.Equals(d.ClassName, typeName, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public IEnumerable<ClassDefinition> ListSchemas(string? filter = null)
        {
            this.EnsureSchemaRootLoaded();

            if (this.schemaRoot == null)
            {
                return Array.Empty<ClassDefinition>();
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                return this.schemaRoot.Data.ToArray();
            }

            return this.schemaRoot.Data
                .Where(d => d.ClassName != null
                            && d.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        /// <summary>
        /// Gets all the known route definitions.
        /// </summary>
        /// <returns>
        /// The <see cref="RouteDefinition"/> enumeration.
        /// </returns>
        public IEnumerable<RouteDefinition> GetRouteDefinitions()
        {
            if (this.routeDefinitions == null)
            {
                lock (this.syncLock)
                {
                    if (this.routeDefinitions == null)
                    {
                        var routes = LoadRouteDefinitionsAsync().GetAwaiter().GetResult();
                        this.routeDefinitions = new Collection<RouteDefinition>(routes);
                    }
                }
            }

            return this.routeDefinitions;
        }

        /// <summary>
        /// Ensures the schema root is loaded from the embedded resource (idempotent, thread-safe).
        /// </summary>
        private void EnsureSchemaRootLoaded()
        {
            if (this.schemaRoot != null)
            {
                return;
            }

            lock (this.syncLock)
            {
                if (this.schemaRoot == null)
                {
                    this.schemaRoot = LoadSchemaRootDefinitionAsync().GetAwaiter().GetResult();
                }
            }
        }

        /// <summary>
        /// The load route definitions async.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private static async Task<List<RouteDefinition>> LoadRouteDefinitionsAsync()
        {
            var json = await ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.Routes);

            var options = new JsonSerializerOptions
                              {
                                  PropertyNameCaseInsensitive = true
                              };

            var routes = JsonSerializer.Deserialize<List<RouteDefinition>>(json, options)
                         ?? throw new InvalidOperationException("Failed to deserialize the JSON content.");

            foreach (var route in routes)
            {
                route.Summary = StripXmlDocMarkers(route.Summary);
                route.Description = StripXmlDocMarkers(route.Description);
            }

            return routes;
        }

        /// <summary>
        /// The load schema root definition async.
        /// </summary>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        private static async Task<SchemaRoot> LoadSchemaRootDefinitionAsync()
        {
            var json = await ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.Dtos);

            var options = new JsonSerializerOptions
                              {
                                  PropertyNameCaseInsensitive = true
                              };

            var root = JsonSerializer.Deserialize<SchemaRoot>(json, options)
                       ?? throw new InvalidOperationException("Failed to deserialize the JSON content.");

            foreach (var cls in root.Data)
            {
                cls.Description = StripXmlDocMarkers(cls.Description);

                foreach (var prop in cls.Properties)
                {
                    prop.Description = StripXmlDocMarkers(prop.Description);
                }
            }

            return root;
        }

        // See DescriptionHygiene for the load-time / write-time sanitisation rule.
        private static string StripXmlDocMarkers(string? description) =>
            DescriptionHygiene.Strip(description);
    }
}
