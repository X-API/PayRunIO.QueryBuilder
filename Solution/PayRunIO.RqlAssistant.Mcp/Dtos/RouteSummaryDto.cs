namespace PayRunIO.RqlAssistant.Mcp.Dtos
{
    using System.ComponentModel;

    /// <summary>
    /// Lightweight wire-format DTO for a route listing (no description or response type).
    /// </summary>
    public sealed class RouteSummaryDto
    {
        [Description("The route class name (unique key), e.g. 'GetEmployeeRoute'. Pass this to get_route for full details.")]
        public string ClassName { get; set; } = string.Empty;

        [Description("HTTP verb, e.g. 'GET', 'POST', 'PUT', 'DELETE', 'PATCH'.")]
        public string Verb { get; set; } = string.Empty;

        [Description("The URL template, e.g. '/Employer/{employerId}/Employee/{employeeId}'.")]
        public string RouteSignature { get; set; } = string.Empty;

        [Description("Short one-line summary of what the route does.")]
        public string Summary { get; set; } = string.Empty;
    }
}
