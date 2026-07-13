namespace PayRunIO.ReportBuilder.Services
{
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Circuit-scoped owner of the in-progress report: the query text, its parsed parameters, the
    /// last execution result/error and the assistant conversation. Hoisting this state out of the
    /// <c>ReportDesigner</c> page (and out of <see cref="ReportChatService"/>'s private list) means it
    /// survives navigation between pages within a circuit, and gives a single serialisable unit that
    /// <see cref="ReportSessionStore"/> mirrors to browser localStorage so it also survives a circuit
    /// teardown (reload, server restart, or the full-page sign-in round-trip after a token refresh).
    /// </summary>
    public sealed class ReportSession
    {
        private readonly List<ChatMessage> history = new();

        private string queryXml = string.Empty;

        private string? savedReportId;

        private string reportName = string.Empty;

        /// <summary>Raised after any mutation so the UI can re-render and persistence can re-save.</summary>
        public event Action? Changed;

        public string QueryXml
        {
            get => this.queryXml;
            set
            {
                this.queryXml = value ?? string.Empty;
                this.Variables = QueryVariables.Parse(this.queryXml);
                this.NotifyChanged();
            }
        }

        public IReadOnlyList<QueryVariable> Variables { get; private set; } = Array.Empty<QueryVariable>();

        /// <summary>API identifier of the report definition this query was loaded from or saved to;
        /// null while the report has never been saved. Saving a null-id session creates a new
        /// definition, otherwise the existing one is updated.</summary>
        public string? SavedReportId
        {
            get => this.savedReportId;
            set
            {
                this.savedReportId = string.IsNullOrWhiteSpace(value) ? null : value;
                this.NotifyChanged();
            }
        }

        /// <summary>User-visible report name, without the managed "ReportBuilder-" title prefix.</summary>
        public string ReportName
        {
            get => this.reportName;
            set
            {
                this.reportName = value ?? string.Empty;
                this.NotifyChanged();
            }
        }

        public QueryExecutionResult? Result { get; private set; }

        public string? LastErrorMessage { get; private set; }

        public bool SessionExpired { get; private set; }

        public IReadOnlyList<ChatMessage> History => this.history;

        public void SetVariable(string name, string value) =>
            this.QueryXml = QueryVariables.SetValue(this.queryXml, name, value);

        public void SetResult(QueryExecutionResult? result, string? error, bool sessionExpired)
        {
            this.Result = result;
            this.LastErrorMessage = error;
            this.SessionExpired = sessionExpired;
            this.NotifyChanged();
        }

        public void AddMessage(ChatMessage message)
        {
            this.history.Add(message);
            this.NotifyChanged();
        }

        public void ClearHistory()
        {
            this.history.Clear();
            this.NotifyChanged();
        }

        /// <summary>Snapshot of everything worth persisting. Results are excluded — they are large,
        /// re-derivable by re-running, and reference a live API response; the query is what the user
        /// is editing and must not be lost.</summary>
        public ReportSessionSnapshot ToSnapshot() =>
            new()
                {
                    QueryXml = this.queryXml,
                    SavedReportId = this.savedReportId,
                    ReportName = this.reportName,
                    History = this.history
                        .Select(m => new PersistedMessage { Role = m.Role, Text = m.Text })
                        .ToList()
                };

        /// <summary>Rehydrates from a persisted snapshot without raising <see cref="Changed"/> per step —
        /// callers restore once on load, then trigger a single render.</summary>
        public void Restore(ReportSessionSnapshot snapshot)
        {
            this.queryXml = snapshot.QueryXml ?? string.Empty;
            this.Variables = QueryVariables.Parse(this.queryXml);
            this.savedReportId = string.IsNullOrWhiteSpace(snapshot.SavedReportId) ? null : snapshot.SavedReportId;
            this.reportName = snapshot.ReportName ?? string.Empty;

            this.history.Clear();

            if (snapshot.History != null)
            {
                foreach (var message in snapshot.History)
                {
                    this.history.Add(new ChatMessage { Role = message.Role, Text = message.Text ?? string.Empty });
                }
            }

            this.Result = null;
            this.LastErrorMessage = null;
            this.SessionExpired = false;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(this.queryXml) && this.history.Count == 0;

        private void NotifyChanged() => this.Changed?.Invoke();
    }

    /// <summary>Persistable projection of <see cref="ReportSession"/>. Tool-role turns and tool-call
    /// metadata are intentionally dropped — only the visible User/Assistant/System conversation is
    /// restored; the model rebuilds its own tool context from the query on the next turn.</summary>
    public sealed class ReportSessionSnapshot
    {
        public string? QueryXml { get; set; }

        public string? SavedReportId { get; set; }

        public string? ReportName { get; set; }

        public List<PersistedMessage>? History { get; set; }
    }

    public sealed class PersistedMessage
    {
        public ParticipantType Role { get; set; }

        public string? Text { get; set; }
    }
}
