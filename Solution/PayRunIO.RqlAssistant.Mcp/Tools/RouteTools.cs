namespace PayRunIO.RqlAssistant.Mcp.Tools
{
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol.Server;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Dtos;

    /// <summary>
    /// MCP tools exposing PayRunIO API route lookups. Thin shim over <see cref="RqlToolDispatcher"/>'s
    /// conversion helpers so MCP and the in-process WPF caller share one DTO surface.
    /// </summary>
    [McpServerToolType]
    public static class RouteTools
    {
        [McpServerTool(Name = "list_routes")]
        [Description(RqlToolDescriptions.ListRoutes)]
        public static IEnumerable<RouteSummaryDto> ListRoutes(
            IDocumentRepository repository,
            [Description(RqlToolDescriptions.ListRoutesFilterParam)] string? filter = null,
            [Description(RqlToolDescriptions.ListRoutesVerbParam)] string? verb = null,
            [Description(RqlToolDescriptions.ListRoutesTagParam)] string? tag = null)
        {
            return RqlToolDispatcher.FilterRoutes(repository.GetRouteDefinitions(), filter, verb, tag)
                .Select(RqlToolDispatcher.ToSummary)
                .ToArray();
        }

        [McpServerTool(Name = "get_route")]
        [Description(RqlToolDescriptions.GetRoute)]
        public static RouteDto? GetRoute(
            IDocumentRepository repository,
            [Description(RqlToolDescriptions.GetRouteClassNameParam)] string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return null;
            }

            var route = repository
                .GetRouteDefinitions()
                .FirstOrDefault(r => string.Equals(r.ClassName, className, StringComparison.OrdinalIgnoreCase));

            return route == null ? null : RqlToolDispatcher.ToFull(route);
        }
    }
}
