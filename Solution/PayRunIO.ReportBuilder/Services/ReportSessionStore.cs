namespace PayRunIO.ReportBuilder.Services
{
    using System.Text.Json;

    using Microsoft.JSInterop;

    /// <summary>
    /// Mirrors a <see cref="ReportSession"/> to browser localStorage via the reportBuilder JS helpers,
    /// so an in-progress query and its conversation survive a full circuit teardown (page reload,
    /// server restart, or the full-page sign-in redirect after a token expiry). Saves are debounced so
    /// per-keystroke query edits do not thrash the interop channel.
    /// </summary>
    public sealed class ReportSessionStore
    {
        private const string StorageKey = "payrun.reportbuilder.draft.v1";

        private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);

        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly IJSRuntime jsRuntime;

        private CancellationTokenSource? pendingSave;

        public ReportSessionStore(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime;
        }

        /// <summary>Loads a persisted draft into the session. Returns true when a draft was restored.
        /// Must run after the first render (JS interop is unavailable during prerender).</summary>
        public async Task<bool> TryRestoreAsync(ReportSession session)
        {
            string? json;

            try
            {
                json = await this.jsRuntime.InvokeAsync<string?>("reportBuilder.loadDraft", StorageKey);
            }
            catch (JSException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var snapshot = JsonSerializer.Deserialize<ReportSessionSnapshot>(json, SerializerOptions);

                if (snapshot == null || (string.IsNullOrWhiteSpace(snapshot.QueryXml) && (snapshot.History?.Count ?? 0) == 0))
                {
                    return false;
                }

                session.Restore(snapshot);
                return true;
            }
            catch (JsonException)
            {
                // Corrupt or schema-changed draft — discard it rather than blocking the user.
                await this.ClearAsync();
                return false;
            }
        }

        /// <summary>Debounced save. Repeated calls within the debounce window collapse into one write of
        /// the latest state.</summary>
        public void ScheduleSave(ReportSession session)
        {
            this.pendingSave?.Cancel();
            this.pendingSave?.Dispose();

            var cts = new CancellationTokenSource();
            this.pendingSave = cts;

            _ = this.DebouncedSaveAsync(session, cts.Token);
        }

        /// <summary>Writes the current state immediately, bypassing the debounce. Call this before any
        /// full-page navigation (e.g. the sign-in redirect) — that tears the circuit down, and a
        /// still-pending debounced save would never run, losing the last edits.</summary>
        public async Task FlushSaveAsync(ReportSession session)
        {
            this.pendingSave?.Cancel();

            var json = JsonSerializer.Serialize(session.ToSnapshot(), SerializerOptions);

            try
            {
                await this.jsRuntime.InvokeVoidAsync("reportBuilder.saveDraft", StorageKey, json);
            }
            catch (JSException)
            {
                // Best-effort — nothing to recover if interop is already gone.
            }
        }

        /// <summary>True when a non-empty draft is present in localStorage. Lets the Reports page offer a
        /// "continue where you left off" entry point without loading the whole session.</summary>
        public async Task<bool> HasDraftAsync()
        {
            string? json;

            try
            {
                json = await this.jsRuntime.InvokeAsync<string?>("reportBuilder.loadDraft", StorageKey);
            }
            catch (JSException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var snapshot = JsonSerializer.Deserialize<ReportSessionSnapshot>(json, SerializerOptions);
                return snapshot != null
                       && (!string.IsNullOrWhiteSpace(snapshot.QueryXml) || (snapshot.History?.Count ?? 0) > 0);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public async Task ClearAsync()
        {
            this.pendingSave?.Cancel();

            try
            {
                await this.jsRuntime.InvokeVoidAsync("reportBuilder.clearDraft", StorageKey);
            }
            catch (JSException)
            {
                // Best-effort.
            }
        }

        private async Task DebouncedSaveAsync(ReportSession session, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(SaveDebounce, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            var json = JsonSerializer.Serialize(session.ToSnapshot(), SerializerOptions);

            try
            {
                await this.jsRuntime.InvokeVoidAsync("reportBuilder.saveDraft", StorageKey, json);
            }
            catch (JSException)
            {
                // Interop can fail if the circuit is tearing down — the state is already lost in that
                // case, so there is nothing to recover by surfacing this.
            }
        }
    }
}
