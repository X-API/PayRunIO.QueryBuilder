namespace PayRunIO.RqlAssistant.Mcp.Tools
{
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol.Server;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Dtos;

    /// <summary>
    /// MCP tools exposing PayRunIO entity schema lookups. Thin shim over <see cref="RqlToolDispatcher"/>'s
    /// conversion helpers so MCP and the in-process WPF caller share one DTO surface.
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
                .Select(RqlToolDispatcher.ToSummary)
                .ToArray();
        }

        [McpServerTool(Name = "get_schema")]
        [Description("Get the full definition of a single PayRunIO entity schema, including all of its properties. Use this to ground RQL queries against the real shape of entities like Employee, EmployeeSummary, PayRun, etc. Match is exact and case-insensitive; returns null if the name is unknown.")]
        public static SchemaDto? GetSchema(
            IDocumentRepository repository,
            [Description("The exact schema type name, e.g. 'Employee', 'EmployeeSummary', 'PayRun'. Case-insensitive.")] string typeName)
        {
            var schema = repository.GetSchema(typeName);

            return schema == null ? null : RqlToolDispatcher.ToFull(schema);
        }
    }
}
