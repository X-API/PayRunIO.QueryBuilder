namespace PayRunIO.RqlAssistant.Service.Dtos
{
    using System.ComponentModel;

    public sealed class ValidationDiagnosticDto
    {
        [Description("Severity of the diagnostic: 'Error' or 'Warning'.")]
        public string Severity { get; set; } = string.Empty;

        [Description("1-based line number where the issue was detected, or 0 if unknown.")]
        public int Line { get; set; }

        [Description("1-based column number where the issue was detected, or 0 if unknown.")]
        public int Column { get; set; }

        [Description("Short machine-readable code identifying the category of issue, e.g. 'XsdValidation', 'MalformedXml', 'EmptyInput'.")]
        public string Code { get; set; } = string.Empty;

        [Description("Human-readable diagnostic message.")]
        public string Message { get; set; } = string.Empty;
    }
}
