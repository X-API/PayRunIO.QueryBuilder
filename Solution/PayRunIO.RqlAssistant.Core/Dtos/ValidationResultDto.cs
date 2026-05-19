namespace PayRunIO.RqlAssistant.Service.Dtos
{
    using System.ComponentModel;

    public sealed class ValidationResultDto
    {
        [Description("True when no Error-level diagnostics were produced. Warnings do not invalidate the query.")]
        public bool IsValid { get; set; }

        [Description("All diagnostics produced during validation, in document order.")]
        public ValidationDiagnosticDto[] Diagnostics { get; set; } = System.Array.Empty<ValidationDiagnosticDto>();
    }
}
