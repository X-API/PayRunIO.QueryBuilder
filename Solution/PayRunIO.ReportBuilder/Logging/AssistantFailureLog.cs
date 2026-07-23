namespace PayRunIO.ReportBuilder.Logging
{
    using System.Text;

    using log4net;
    using log4net.Core;

    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Records what the RQL assistant got wrong.
    ///
    /// The server side validation gate already catches and retries bad queries, so these failures
    /// are invisible to users — and therefore invisible to development unless recorded. Diagnostics
    /// that the correction loop resolved show which rules the model routinely trips over; those it
    /// could not resolve are outright gaps in the grounding data.
    /// </summary>
    public sealed class AssistantFailureLog
    {
        private static readonly ILog Log = LogManager.GetLogger("PayRunIO.ReportBuilder.Assistant");

        private readonly DiagnosticContext diagnostics;

        public AssistantFailureLog(DiagnosticContext diagnostics) => this.diagnostics = diagnostics;

        /// <summary>
        /// Records diagnostics raised against a query the assistant produced, before any correction
        /// is attempted. Logged at info: this is normal operation, and the value is in the aggregate
        /// — which codes appear most often across many sessions.
        ///
        /// The message carries the full text of every diagnostic and the query that provoked them,
        /// not just the codes. A log line that only names a rule cannot be acted on without pulling
        /// the structured fields up separately, which is exactly the round trip that stops failure
        /// trends being reviewed at all.
        /// </summary>
        /// <param name="attempt">The correction round, starting at 1.</param>
        /// <param name="diagnosticList">The diagnostics raised against the generated query.</param>
        /// <param name="queryXml">The generated query that failed validation.</param>
        /// <param name="prompt">The prompt that produced the failing query, if available.</param>
        public void ValidationFailed(
            int attempt,
            IReadOnlyList<ValidationDiagnostic> diagnosticList,
            string queryXml,
            string? prompt = null)
        {
            var properties = this.DiagnosticProperties(queryXml, diagnosticList);

            properties["correctionAttempt"] = attempt;
            properties["prompt"] = prompt;

            StructuredLog.Write(
                Log,
                Level.Info,
                $"Assistant query failed validation on attempt {attempt} with "
                + $"{diagnosticList.Count} diagnostic(s) [{Summarise(diagnosticList)}]."
                + Detail(diagnosticList, queryXml, prompt),
                properties);
        }

        /// <summary>
        /// Records diagnostics that survived every correction attempt. These reached the user, and
        /// each one is a concrete grounding gap — the model was told exactly what was wrong and
        /// still could not fix it. Logged at warn so they can be alerted on.
        /// </summary>
        /// <param name="diagnosticList">The diagnostics still outstanding.</param>
        /// <param name="correctionsApplied">How many correction round trips were spent.</param>
        /// <param name="queryXml">The final query as surfaced to the user.</param>
        /// <param name="prompt">The prompt that started the turn, if available.</param>
        public void ValidationUnresolved(
            IReadOnlyList<ValidationDiagnostic> diagnosticList,
            int correctionsApplied,
            string? queryXml,
            string? prompt = null)
        {
            var properties = this.DiagnosticProperties(queryXml, diagnosticList);

            properties["correctionsApplied"] = correctionsApplied;
            properties["prompt"] = prompt;

            StructuredLog.Write(
                Log,
                Level.Warn,
                $"Assistant query still has {diagnosticList.Count} unresolved diagnostic(s) after "
                + $"{correctionsApplied} correction attempt(s) [{Summarise(diagnosticList)}]."
                + Detail(diagnosticList, queryXml, prompt),
                properties);
        }

        /// <summary>
        /// Records a query that was corrected successfully, so the proportion of turns needing
        /// correction can be tracked over time as the grounding data improves.
        /// </summary>
        /// <param name="correctionsApplied">How many correction round trips were spent.</param>
        /// <param name="resolvedDiagnostics">
        /// The diagnostics the correction loop cleared. Recorded because a rule that is repeatedly
        /// broken and then repeatedly fixed is still a grounding gap — the model only got there on
        /// the second ask — and without these the recovered case is indistinguishable from a clean
        /// first attempt in the aggregate.
        /// </param>
        /// <param name="queryXml">The corrected query.</param>
        public void ValidationRecovered(
            int correctionsApplied,
            IReadOnlyList<ValidationDiagnostic>? resolvedDiagnostics = null,
            string? queryXml = null)
        {
            var diagnosticList = resolvedDiagnostics ?? Array.Empty<ValidationDiagnostic>();

            var properties = this.DiagnosticProperties(queryXml, diagnosticList);

            properties["correctionsApplied"] = correctionsApplied;

            StructuredLog.Write(
                Log,
                Level.Info,
                $"Assistant query failed validation but was corrected after {correctionsApplied} attempt(s)"
                + (diagnosticList.Count == 0
                       ? "."
                       : $" [{Summarise(diagnosticList)}].{Detail(diagnosticList, queryXml, prompt: null)}"),
                properties);
        }

        /// <summary>
        /// Records a failed call to the AI provider — an outage, a rate limit, or a bad request.
        /// Distinct from a validation failure: nothing is wrong with the grounding data, so these
        /// must not be counted as assistant quality problems.
        /// </summary>
        /// <param name="exception">The provider exception.</param>
        /// <param name="phase">Which call failed: the user's turn, or an automatic correction.</param>
        public void ProviderFailed(Exception exception, string phase)
        {
            var properties = this.BaseProperties();

            properties["phase"] = phase;

            StructuredLog.Write(
                Log,
                Level.Error,
                $"AI provider request failed during {phase} - {exception.GetType().Name}: {exception.Message}",
                properties,
                exception);
        }

        /// <summary>
        /// Records a reply that contained no XML block at all, so no query could be extracted. Often
        /// means the model answered conversationally when a query was expected — which can only be
        /// confirmed by reading the reply, so the reply text is recorded with it.
        ///
        /// The reply goes in the message only. There is no query to separate out of it here, so a
        /// matching property would just be a second copy of the same text.
        /// </summary>
        /// <param name="prompt">The prompt that was asked.</param>
        /// <param name="response">The reply that contained no query block.</param>
        public void NoQueryProduced(string prompt, string? response = null)
        {
            var properties = this.BaseProperties();

            properties["prompt"] = prompt;
            properties["responseLength"] = response?.Length;

            StructuredLog.Write(
                Log,
                Level.Info,
                "Assistant reply contained no query block."
                + $"\nPrompt: {prompt}"
                + (response == null ? string.Empty : $"\nReply: {response}"),
                properties);
        }

        /// <summary>
        /// A compact, aggregatable rendering: the diagnostic codes and severities only. Kept
        /// alongside the full detail so BetterStack still has a short, stable fragment to group on.
        /// </summary>
        private static string Summarise(IReadOnlyList<ValidationDiagnostic> diagnosticList) =>
            string.Join(", ", diagnosticList.Select(d => $"{d.Severity}/{d.Code}").Distinct());

        /// <summary>
        /// The human readable body of a validation failure message: every diagnostic in full, the
        /// query they were raised against, and the prompt that asked for it.
        ///
        /// The diagnostics and the prompt duplicate what the structured properties carry,
        /// deliberately: the properties are what aggregate queries run against, while the message is
        /// what a person reads when a failure is opened, and a failure that cannot be understood
        /// from the log line on its own tends not to be investigated at all.
        ///
        /// The query body is the exception. It is the largest part of the event by some margin and
        /// travels verbatim in the "queryXml" property, so the message notes its presence and size
        /// rather than repeating it. The line stays self-describing — the diagnostics name the lines
        /// they were raised against — without paying for the query twice.
        /// </summary>
        private static string Detail(
            IReadOnlyList<ValidationDiagnostic> diagnosticList,
            string? queryXml,
            string? prompt)
        {
            var builder = new StringBuilder();

            foreach (var diagnostic in diagnosticList)
            {
                builder.Append($"\n  [{diagnostic.Severity}] {diagnostic.Code} (line {diagnostic.Line}): {diagnostic.Message}");
            }

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                builder.Append($"\nPrompt: {prompt}");
            }

            if (!string.IsNullOrWhiteSpace(queryXml))
            {
                builder.Append($"\nQuery: [redacted — see the queryXml property, {queryXml.Length} chars]");
            }

            return builder.ToString();
        }

        private Dictionary<string, object?> BaseProperties() =>
            new()
                {
                    ["correlationId"] = this.diagnostics.CorrelationId,
                    ["userSubject"] = this.diagnostics.UserSubject,
                };

        private Dictionary<string, object?> DiagnosticProperties(
            string? queryXml,
            IReadOnlyList<ValidationDiagnostic> diagnosticList)
        {
            var properties = this.BaseProperties();

            // Left absent rather than empty when there are no diagnostics: an empty string is a
            // value that aggregate queries would count, and a provider failure or a clean recovery
            // must not appear in "which rules get broken" analysis at all.
            if (diagnosticList.Count > 0)
            {
                properties["diagnosticCodes"] = string.Join(",", diagnosticList.Select(d => d.Code).Distinct());
                properties["diagnostics"] =
                    string.Join(" | ", diagnosticList.Select(d => $"[{d.Severity}] {d.Code} (line {d.Line}): {d.Message}"));
                properties["diagnosticCount"] = diagnosticList.Count;
            }

            properties["queryXml"] = queryXml;

            return properties;
        }
    }
}
