namespace PayRunIO.ReportBuilder.Services
{
    using System.Text;
    using System.Text.RegularExpressions;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Models;

    public sealed record ChatTurnResult(string? UpdatedQueryXml, string? Error);

    /// <summary>
    /// Per-circuit conversation state over the RQL assistant. Each turn passes the current report
    /// query (and any execution error) as context so the model can amend rather than restart, and
    /// the reply's final XML block is surfaced as the updated report query. The XML is gated
    /// server-side: any validation or lint diagnostics are fed back to the model for correction
    /// before the query reaches the user.
    /// </summary>
    public sealed class ReportChatService
    {
        /// <summary>
        /// Maximum server-side correction round trips per turn. Diagnostics that survive two
        /// dedicated fix requests are unlikely to be resolved by a third; they are surfaced to the
        /// user instead.
        /// </summary>
        private const int MaxCorrectionAttempts = 2;

        private static readonly Regex XmlBlockRegex = new(
            "```xml\\s*([\\s\\S]*?)\\s*```",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IRqlRagService rqlRagService;

        private readonly IRqlQueryReviewer queryReviewer;

        private readonly ReportSession session;

        public ReportChatService(IRqlRagService rqlRagService, IRqlQueryReviewer queryReviewer, ReportSession session)
        {
            this.rqlRagService = rqlRagService;
            this.queryReviewer = queryReviewer;
            this.session = session;
        }

        public IReadOnlyList<ChatMessage> History => this.session.History;

        public async Task<ChatTurnResult> AskAsync(
            string prompt,
            string? currentQueryXml,
            string? lastError,
            Action<string>? onActivity = null,
            CancellationToken cancellationToken = default)
        {
            // Snapshot the history before adding the new question: AskQuestion appends the prompt
            // as the final user turn itself, so including it would show the model the question twice.
            var modelHistory = this.session.History.Where(m => m.Role != ParticipantType.System).ToList();

            this.session.AddMessage(new ChatMessage { Role = ParticipantType.User, Text = prompt });

            var effectivePrompt = BuildPrompt(prompt, currentQueryXml, lastError);

            string response;

            try
            {
                response = await this.rqlRagService.AskQuestion(
                               effectivePrompt,
                               chatHistory: modelHistory,
                               format: ResponseType.TabularQuery,
                               onActivity: onActivity,
                               cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                const string Cancelled = "The assistant request was cancelled — no changes were made to the report.";

                this.session.AddMessage(new ChatMessage { Role = ParticipantType.System, Text = Cancelled });

                return new ChatTurnResult(null, Cancelled);
            }
            catch (Exception exception)
            {
                var error = $"[{exception.GetType().Name}] The AI assistant request failed: {exception.Message}";

                this.session.AddMessage(new ChatMessage { Role = ParticipantType.System, Text = error });

                return new ChatTurnResult(null, error);
            }

            var xml = ExtractLastXmlBlock(response);
            var correctionsApplied = 0;

            // Server-side validation gate: the intermediate fix cycles stay out of the visible
            // history; only the final reply and a summary note are shown.
            for (var attempt = 0; xml != null && attempt < MaxCorrectionAttempts; attempt++)
            {
                onActivity?.Invoke("Checking the query against the schema and route rules");

                var diagnostics = this.queryReviewer.Review(xml);

                if (diagnostics.Count == 0)
                {
                    break;
                }

                modelHistory.Add(new ChatMessage { Role = ParticipantType.User, Text = effectivePrompt });
                modelHistory.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = response });

                effectivePrompt = BuildCorrectionPrompt(diagnostics);

                onActivity?.Invoke(
                    $"Fixing {diagnostics.Count} validation issue(s) — attempt {attempt + 1} of {MaxCorrectionAttempts}");

                try
                {
                    response = await this.rqlRagService.AskQuestion(
                                   effectivePrompt,
                                   chatHistory: modelHistory,
                                   format: ResponseType.TabularQuery,
                                   onActivity: onActivity,
                                   cancellationToken: cancellationToken);
                }
                catch (Exception)
                {
                    // Correction is best-effort (including when the user cancels mid-fix): fall back
                    // to the last reply and let the remaining diagnostics be reported to the user below.
                    response = modelHistory[^1].Text;
                    break;
                }

                xml = ExtractLastXmlBlock(response) ?? xml;
                correctionsApplied++;
            }

            this.session.AddMessage(new ChatMessage { Role = ParticipantType.Assistant, Text = response });

            this.AppendValidationNote(xml, correctionsApplied);

            return new ChatTurnResult(xml, null);
        }

        private void AppendValidationNote(string? xml, int correctionsApplied)
        {
            var remaining = xml == null
                                ? Array.Empty<ValidationDiagnostic>() as IReadOnlyList<ValidationDiagnostic>
                                : this.queryReviewer.Review(xml);

            if (remaining.Count > 0)
            {
                var summary = string.Join(
                    "\n",
                    remaining.Select(d => $"* **{d.Severity}** ({d.Code}): {d.Message}"));

                this.session.AddMessage(new ChatMessage
                    {
                        Role = ParticipantType.System,
                        Text = $"The query still has {remaining.Count} validation issue(s) after "
                               + $"{correctionsApplied} automatic correction attempt(s) — review before running:\n{summary}"
                    });
            }
            else if (correctionsApplied > 0)
            {
                this.session.AddMessage(new ChatMessage
                    {
                        Role = ParticipantType.System,
                        Text = "The generated query initially failed validation and was automatically corrected. "
                               + "It now passes all schema, route and property checks."
                    });
            }
        }

        private static string BuildCorrectionPrompt(IReadOnlyList<ValidationDiagnostic> diagnostics)
        {
            var builder = new StringBuilder();

            builder.AppendLine("The RQL query in your last reply failed validation with the following diagnostics:");

            foreach (var diagnostic in diagnostics)
            {
                builder.AppendLine($"* [{diagnostic.Severity}] {diagnostic.Code} (line {diagnostic.Line}): {diagnostic.Message}");
            }

            builder.AppendLine();
            builder.Append(
                "Resolve every diagnostic, including warnings — use the lookup tools to find correct routes, "
                + "property names and syntax, verify the fix with validate_query, and reply with the complete corrected query.");

            return builder.ToString();
        }

        public void Reset() => this.session.ClearHistory();

        private static string BuildPrompt(string prompt, string? currentQueryXml, string? lastError)
        {
            if (string.IsNullOrWhiteSpace(currentQueryXml) && string.IsNullOrWhiteSpace(lastError))
            {
                return prompt;
            }

            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(currentQueryXml))
            {
                builder.AppendLine("This is the current report query:");
                builder.AppendLine("```xml");
                builder.AppendLine(currentQueryXml.Trim());
                builder.AppendLine("```");
                builder.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(lastError))
            {
                builder.AppendLine("The last attempt to execute the report query failed with this error:");
                builder.AppendLine(lastError.Trim());
                builder.AppendLine();
            }

            builder.Append(prompt);

            return builder.ToString();
        }

        private static string? ExtractLastXmlBlock(string response)
        {
            var matches = XmlBlockRegex.Matches(response);

            return matches.Count > 0 ? matches[^1].Groups[1].Value : null;
        }
    }
}
