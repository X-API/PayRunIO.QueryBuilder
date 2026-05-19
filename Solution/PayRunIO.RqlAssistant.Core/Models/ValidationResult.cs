namespace PayRunIO.RqlAssistant.Service.Models
{
    using System.Collections.Generic;
    using System.Linq;

    public sealed class ValidationResult
    {
        public bool IsValid => this.Diagnostics.All(d => d.Severity != ValidationSeverity.Error);

        public List<ValidationDiagnostic> Diagnostics { get; set; } = new List<ValidationDiagnostic>();
    }
}
