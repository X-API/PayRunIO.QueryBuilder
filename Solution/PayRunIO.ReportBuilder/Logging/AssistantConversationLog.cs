namespace PayRunIO.ReportBuilder.Logging
{
    using log4net;
    using log4net.Core;

    /// <summary>
    /// Records the assistant conversation itself — what the user asked, what was actually sent to
    /// the model, and what came back.
    ///
    /// The failure logs record that something went wrong; this records the exchange that produced
    /// it. Improving the grounding data needs both: a diagnostic code says which rule was broken,
    /// but only the prompt and the reply show what the model was reaching for when it broke it.
    ///
    /// Written at debug throughout. These events are high volume and carry the full text of both
    /// sides of the conversation, so they are intended to be enabled while failure trends are being
    /// investigated and turned down again afterwards by raising the logger level in log4net.config.
    /// </summary>
    public sealed class AssistantConversationLog
    {
        /// <summary>
        /// A separate logger from the failure logs so the conversation transcript can be silenced
        /// independently — set this logger to INFO in log4net.config and the failure events carry on
        /// unaffected.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger("PayRunIO.ReportBuilder.Conversation");

        private readonly DiagnosticContext diagnostics;

        public AssistantConversationLog(DiagnosticContext diagnostics) => this.diagnostics = diagnostics;

        /// <summary>
        /// Whether conversation logging is switched on. Checked before building the larger payloads
        /// so a disabled transcript costs nothing beyond the level check.
        /// </summary>
        public bool IsEnabled => Log.Logger.IsEnabledFor(Level.Debug);

        /// <summary>
        /// Records the user's question as typed, alongside the prompt actually sent to the model.
        /// The two differ whenever the current query or a previous execution error is folded in, and
        /// that added context is frequently what steers the model wrong — so both are kept.
        /// </summary>
        /// <param name="prompt">The question exactly as the user typed it.</param>
        /// <param name="effectivePrompt">The composed prompt sent to the model.</param>
        /// <param name="currentQueryXml">The query in the designer when the question was asked.</param>
        /// <param name="lastError">The execution error being carried into the turn, if any.</param>
        /// <param name="historyCount">How many prior turns were replayed to the model.</param>
        public void UserRequest(
            string prompt,
            string effectivePrompt,
            string? currentQueryXml,
            string? lastError,
            int historyCount)
        {
            var properties = this.BaseProperties();

            properties["eventKind"] = "UserRequest";
            properties["prompt"] = prompt;
            properties["effectivePrompt"] = effectivePrompt;
            properties["currentQueryXml"] = currentQueryXml;
            properties["lastError"] = lastError;
            properties["historyCount"] = historyCount;

            StructuredLog.Write(
                Log,
                Level.Debug,
                $"User request ({prompt.Length} chars, {historyCount} prior turn(s)"
                + $"{(lastError == null ? string.Empty : ", carrying an execution error")}): {prompt}",
                properties);
        }

        /// <summary>
        /// Records the model's reply and the query extracted from it. A reply whose prose and XML
        /// disagree is a distinct failure mode from one that simply produced an invalid query, and
        /// it is only visible from the reply text.
        ///
        /// The reply travels in the message rather than as a property, with the query redacted out
        /// of it. The query is carried separately by "extractedQueryXml", so between the two the
        /// whole reply is still recoverable — but each half is stored exactly once.
        /// </summary>
        /// <param name="response">The complete reply text.</param>
        /// <param name="extractedQueryXml">The query pulled out of the reply, or null if none.</param>
        /// <param name="phase">Which call produced the reply: the user's turn, or a correction.</param>
        /// <param name="elapsed">How long the provider call took.</param>
        public void AssistantResponse(
            string response,
            string? extractedQueryXml,
            string phase,
            TimeSpan elapsed)
        {
            var properties = this.BaseProperties();

            properties["eventKind"] = "AssistantResponse";
            properties["phase"] = phase;
            properties["extractedQueryXml"] = extractedQueryXml;
            properties["queryExtracted"] = extractedQueryXml != null;
            properties["responseLength"] = response.Length;
            properties["elapsedMs"] = (long)elapsed.TotalMilliseconds;

            StructuredLog.Write(
                Log,
                Level.Debug,
                $"Assistant response ({phase}, {response.Length} chars, {elapsed.TotalMilliseconds:F0}ms, "
                + $"query {(extractedQueryXml == null ? "not extracted" : "extracted")}): "
                + Redact(response, extractedQueryXml),
                properties);
        }

        /// <summary>
        /// Removes the extracted query from the message text, leaving a placeholder in its place.
        ///
        /// The query is already shipped in full as the "extractedQueryXml" property, and a query is
        /// typically the bulk of a reply, so repeating it inside the rendered message would store
        /// the largest part of every turn twice. What the message keeps is the prose around it —
        /// the part no property carries, and the part that shows what the model thought it was
        /// doing. Nothing is lost: the two halves rejoin on the same event.
        /// </summary>
        /// <param name="response">The complete reply text.</param>
        /// <param name="extractedQueryXml">The query to redact, or null to leave the reply intact.</param>
        private static string Redact(string response, string? extractedQueryXml) =>
            string.IsNullOrWhiteSpace(extractedQueryXml)
                ? response
                : response.Replace(extractedQueryXml, "[query redacted — see the extractedQueryXml property]");

        /// <summary>
        /// Records the correction prompt built from a set of diagnostics. Pairing the instruction
        /// with the reply it produced shows whether a repeated failure is the model ignoring the
        /// diagnostic or the diagnostic message not saying enough to act on — a distinction that
        /// decides whether the fix belongs in the grounding data or in the linter's wording.
        /// </summary>
        /// <param name="attempt">The correction round, starting at 1.</param>
        /// <param name="correctionPrompt">The prompt sent asking for the fix.</param>
        public void CorrectionRequest(int attempt, string correctionPrompt)
        {
            var properties = this.BaseProperties();

            properties["eventKind"] = "CorrectionRequest";
            properties["correctionAttempt"] = attempt;
            properties["effectivePrompt"] = correctionPrompt;

            StructuredLog.Write(
                Log,
                Level.Debug,
                $"Correction request (attempt {attempt}): {correctionPrompt}",
                properties);
        }

        /// <summary>
        /// Records the query handed back to the designer at the end of a turn, after every
        /// correction has been applied. This is the text the user sees, so it is the baseline any
        /// subsequent execution failure should be read against.
        /// </summary>
        /// <param name="queryXml">The final query, or null if the turn produced none.</param>
        /// <param name="correctionsApplied">How many correction round trips were spent.</param>
        /// <param name="outstandingDiagnostics">How many diagnostics remain against the final query.</param>
        public void TurnCompleted(string? queryXml, int correctionsApplied, int outstandingDiagnostics)
        {
            var properties = this.BaseProperties();

            properties["eventKind"] = "TurnCompleted";
            properties["queryXml"] = queryXml;
            properties["correctionsApplied"] = correctionsApplied;
            properties["outstandingDiagnostics"] = outstandingDiagnostics;

            StructuredLog.Write(
                Log,
                Level.Debug,
                $"Turn completed: {correctionsApplied} correction(s) applied, "
                + $"{outstandingDiagnostics} diagnostic(s) outstanding, "
                + $"query {(queryXml == null ? "not produced" : $"{queryXml.Length} chars")}.",
                properties);
        }

        private Dictionary<string, object?> BaseProperties() =>
            new()
                {
                    ["correlationId"] = this.diagnostics.CorrelationId,
                    ["userSubject"] = this.diagnostics.UserSubject,
                    ["prioIdentity"] = this.diagnostics.PrioIdentity,
                };
    }
}
