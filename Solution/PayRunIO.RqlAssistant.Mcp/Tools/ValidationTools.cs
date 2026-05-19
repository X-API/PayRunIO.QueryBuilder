namespace PayRunIO.RqlAssistant.Mcp.Tools
{
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol.Server;

    using PayRunIO.RqlAssistant.Mcp.Dtos;
    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Models;

    [McpServerToolType]
    public static class ValidationTools
    {
        [McpServerTool(Name = "validate_query")]
        [Description("Validate a candidate RQL <Query> XML document against the PayRunIO QuerySchema.xsd. Returns structured diagnostics (line, column, code, message) so a caller can fix the query and retry. IsValid is true only when no Error-level diagnostics are produced; Warnings do not invalidate the query.")]
        public static ValidationResultDto ValidateQuery(
            IQueryValidator validator,
            [Description("The full RQL query XML to validate, starting at the <Query> root element.")] string xml)
        {
            var result = validator.Validate(xml);

            return new ValidationResultDto
                {
                    IsValid = result.IsValid,
                    Diagnostics = result.Diagnostics.Select(ToDto).ToArray()
                };
        }

        private static ValidationDiagnosticDto ToDto(ValidationDiagnostic diagnostic) =>
            new ValidationDiagnosticDto
                {
                    Severity = diagnostic.Severity.ToString(),
                    Line = diagnostic.Line,
                    Column = diagnostic.Column,
                    Code = diagnostic.Code,
                    Message = diagnostic.Message
                };
    }
}
