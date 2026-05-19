namespace PayRunIO.RqlAssistant.Service.Dtos
{
    using System.ComponentModel;

    /// <summary>
    /// Full wire-format DTO for a single PayRunIO API route.
    /// </summary>
    public sealed class RouteDto
    {
        [Description("The route class name (unique key), e.g. 'GetEmployeeRoute'.")]
        public string ClassName { get; set; } = string.Empty;

        [Description("HTTP verb, e.g. 'GET', 'POST', 'PUT', 'DELETE', 'PATCH'.")]
        public string Verb { get; set; } = string.Empty;

        [Description("The URL template, e.g. '/Employer/{employerId}/Employee/{employeeId}'.")]
        public string RouteSignature { get; set; } = string.Empty;

        [Description("The Swagger/OpenAPI operation id.")]
        public string OperationId { get; set; } = string.Empty;

        [Description("Short one-line summary of what the route does.")]
        public string Summary { get; set; } = string.Empty;

        [Description("Longer human-readable description of the route behaviour.")]
        public string Description { get; set; } = string.Empty;

        [Description("The schema type name of the response body, e.g. 'Employee', 'LinkCollection', 'Object'. Cross-reference with get_schema for shape.")]
        public string ResponseType { get; set; } = string.Empty;

        [Description("Grouping tags (typically the primary entity types involved).")]
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    }
}
