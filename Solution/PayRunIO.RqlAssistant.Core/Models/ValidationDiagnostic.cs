namespace PayRunIO.RqlAssistant.Service.Models
{
    public enum ValidationSeverity
    {
        Error,
        Warning
    }

    public sealed class ValidationDiagnostic
    {
        public ValidationSeverity Severity { get; set; }

        public int Line { get; set; }

        public int Column { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
