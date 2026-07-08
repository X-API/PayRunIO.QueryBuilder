namespace PayRunIO.RqlAssistant.Mcp.Tools
{
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol.Server;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Dtos;

    [McpServerToolType]
    public static class ValidationTools
    {
        [McpServerTool(Name = "validate_query")]
        [Description(RqlToolDescriptions.ValidateQuery)]
        public static ValidationResultDto ValidateQuery(
            IQueryValidator validator,
            IRqlSemanticLinter semanticLinter,
            [Description(RqlToolDescriptions.ValidateQueryXmlParam)] string xml)
        {
            var result = validator.Validate(xml);

            // Semantic lint warnings ride along with the XSD diagnostics; they never affect IsValid.
            var lintDiagnostics = semanticLinter.Lint(xml);

            return new ValidationResultDto
                {
                    IsValid = result.IsValid,
                    Diagnostics = result.Diagnostics.Concat(lintDiagnostics).Select(RqlToolDispatcher.ToDto).ToArray()
                };
        }
    }
}
