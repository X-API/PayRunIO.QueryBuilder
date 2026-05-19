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
        [Description("List PayRunIO API routes. Returns class name, verb, URL template and a short summary — call get_route for the full description and response type. Filters are optional and ANDed together.")]
        public static IEnumerable<RouteSummaryDto> ListRoutes(
            IDocumentRepository repository,
            [Description("Optional case-insensitive substring filter applied to the route URL template (RouteSignature). E.g. 'Employee' matches '/Employer/{employerId}/Employee/{employeeId}'.")] string? filter = null,
            [Description("Optional HTTP verb filter, case-insensitive exact match. E.g. 'GET', 'POST', 'PUT', 'DELETE', 'PATCH'.")] string? verb = null,
            [Description("Optional tag filter, case-insensitive exact match against any tag on the route. E.g. 'Employee', 'PayRun', 'Reports'.")] string? tag = null)
        {
            var routes = repository.GetRouteDefinitions();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                routes = routes.Where(r => r.RouteSignature != null
                                           && r.RouteSignature.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(verb))
            {
                routes = routes.Where(r => string.Equals(r.Verb, verb, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                routes = routes.Where(r => r.Tags != null
                                           && r.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
            }

            return routes.Select(RqlToolDispatcher.ToSummary).ToArray();
        }

        [McpServerTool(Name = "get_route")]
        [Description("Get the full definition of a single PayRunIO API route by its class name (the unique key returned by list_routes). Match is exact and case-insensitive; returns null if the class name is unknown.")]
        public static RouteDto? GetRoute(
            IDocumentRepository repository,
            [Description("The exact route class name, e.g. 'GetEmployeeRoute', 'GetAEAssessmentFromEmployeeRoute'. Case-insensitive.")] string className)
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
