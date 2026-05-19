namespace PayRunIO.RqlAssistant.Mcp.Tools
{
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol.Server;

    using PayRunIO.RqlAssistant.Mcp.Dtos;
    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// MCP tools exposing PayRunIO entity schema lookups, backed by <see cref="IDocumentRepository"/>.
    /// </summary>
    [McpServerToolType]
    public static class SchemaTools
    {
        [McpServerTool(Name = "list_schemas")]
        [Description("List all PayRunIO entity schemas. Returns name and description only — call get_schema for full property details. Optionally filter by a case-insensitive substring match on the schema name.")]
        public static IEnumerable<SchemaSummaryDto> ListSchemas(
            IDocumentRepository repository,
            [Description("Optional case-insensitive substring filter applied to schema names. Omit or pass an empty string to list all schemas.")] string? filter = null)
        {
            return repository
                .ListSchemas(filter)
                .Select(ToSummary)
                .ToArray();
        }

        [McpServerTool(Name = "get_schema")]
        [Description("Get the full definition of a single PayRunIO entity schema, including all of its properties. Use this to ground RQL queries against the real shape of entities like Employee, EmployeeSummary, PayRun, etc. Match is exact and case-insensitive; returns null if the name is unknown.")]
        public static SchemaDto? GetSchema(
            IDocumentRepository repository,
            [Description("The exact schema type name, e.g. 'Employee', 'EmployeeSummary', 'PayRun'. Case-insensitive.")] string typeName)
        {
            var schema = repository.GetSchema(typeName);

            return schema == null ? null : ToFull(schema);
        }

        private static SchemaSummaryDto ToSummary(ClassDefinition schema) =>
            new SchemaSummaryDto
                {
                    Name = schema.ClassName ?? string.Empty,
                    Description = schema.Description ?? string.Empty
                };

        private static SchemaDto ToFull(ClassDefinition schema) =>
            new SchemaDto
                {
                    Name = schema.ClassName ?? string.Empty,
                    Description = schema.Description ?? string.Empty,
                    Properties = (schema.Properties ?? new List<PropertyDefinition>())
                        .Select(p => new PropertyDto
                            {
                                Name = p.Name ?? string.Empty,
                                Type = p.Type ?? string.Empty,
                                Description = p.Description ?? string.Empty
                            })
                        .ToArray()
                };
    }
}
