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
        [Description(RqlToolDescriptions.ListSchemas)]
        public static IEnumerable<SchemaSummaryDto> ListSchemas(
            IDocumentRepository repository,
            [Description(RqlToolDescriptions.ListSchemasFilterParam)] string? filter = null)
        {
            return repository
                .ListSchemas(filter)
                .Select(RqlToolDispatcher.ToSummary)
                .ToArray();
        }

        [McpServerTool(Name = "get_schema")]
        [Description(RqlToolDescriptions.GetSchema)]
        public static SchemaDto? GetSchema(
            IDocumentRepository repository,
            [Description(RqlToolDescriptions.GetSchemaTypeNameParam)] string typeName)
        {
            var schema = repository.GetSchema(typeName);

            return schema == null ? null : RqlToolDispatcher.ToFull(schema);
        }
    }
}
