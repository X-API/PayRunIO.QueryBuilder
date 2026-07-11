namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Combines XSD validation and semantic linting — the same checks the 'validate_query' tool
    /// performs. Lets a host gate the model's final XML reply server-side instead of trusting the
    /// model to have validated (and acted on the diagnostics) itself.
    /// </summary>
    public interface IRqlQueryReviewer
    {
        IReadOnlyList<ValidationDiagnostic> Review(string xml);
    }

    public sealed class RqlQueryReviewer : IRqlQueryReviewer
    {
        private readonly IQueryValidator validator;

        private readonly IRqlSemanticLinter linter;

        public RqlQueryReviewer(IQueryValidator validator, IRqlSemanticLinter linter)
        {
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.linter = linter ?? throw new ArgumentNullException(nameof(linter));
        }

        public IReadOnlyList<ValidationDiagnostic> Review(string xml) =>
            this.validator.Validate(xml).Diagnostics
                .Concat(this.linter.Lint(xml))
                .ToList();
    }
}
