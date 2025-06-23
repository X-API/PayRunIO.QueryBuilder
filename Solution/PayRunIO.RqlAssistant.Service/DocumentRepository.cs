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
        /// Gets all the known route definitions.
        /// </summary>
        /// <returns>
        /// The <see cref="RouteDefinition"/> enumeration.
        /// </returns>
        IEnumerable<RouteDefinition> GetRouteDefinitions();

        /// <summary>
        /// The get documentation method. Gets the RQL documentation for the specified format type: JSON or XML.
        /// </summary>
        /// <param name="formatType">
        /// The format type. Either JSON or XML.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        string GetDocumentation(string formatType);
    }

    /// <summary>
    /// The document repository.
    /// </summary>
    internal class DocumentRepository : IDocumentRepository
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
            if (this.schemaRoot == null)
            {
                lock (this.syncLock)
                {
                    if (this.schemaRoot == null)
                    {
                        this.schemaRoot = LoadSchemaRootDefinitionAsync().GetAwaiter().GetResult();
                    }
                }
            }

            if (this.schemaRoot == null)
            {
                return Array.Empty<string>();
            }

            var filteredList = this.schemaRoot.Data.Where(d => schemaNames.Contains(d.ClassName));

            return filteredList.Select(schema => schema.ToString());
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
        /// The get documentation method. Gets the RQL documentation for the specified format type: JSON or XML.
        /// </summary>
        /// <param name="formatType">
        /// The format type. Either JSON or XML.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string GetDocumentation(string formatType)
        {
            if (string.Equals(formatType, "XML", StringComparison.InvariantCultureIgnoreCase))
            {
                return ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.RqlDocXml).GetAwaiter().GetResult();
            }

            return ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.RqlDocJson).GetAwaiter().GetResult();
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

            return JsonSerializer.Deserialize<List<RouteDefinition>>(json, options)
                   ?? throw new InvalidOperationException("Failed to deserialize the JSON content.");
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

            return JsonSerializer.Deserialize<SchemaRoot>(json, options)
                   ?? throw new InvalidOperationException("Failed to deserialize the JSON content.");
        }
    }
}