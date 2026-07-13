namespace PayRunIO.ReportBuilder.Services
{
    using Microsoft.JSInterop;

    /// <summary>
    /// The two ways the report designer presents the AI-generated query.
    /// </summary>
    public enum DesignerMode
    {
        /// <summary>The RQL stays behind the scenes — the user works purely through the assistant,
        /// parameters and results.</summary>
        Standard,

        /// <summary>The RQL is visible and directly editable, as in the desktop QueryBuilder.</summary>
        Advanced,
    }

    /// <summary>
    /// Circuit-scoped holder of the user's Standard/Advanced designer mode, mirrored to browser
    /// localStorage so the last selection carries over to the next session. Kept separate from the
    /// <see cref="ReportSessionStore"/> draft on purpose: starting a new blank report clears the
    /// draft but must not reset the user's mode preference.
    /// </summary>
    public sealed class DesignerModeStore
    {
        private const string StorageKey = "payrun.reportbuilder.mode.v1";

        private readonly IJSRuntime jsRuntime;

        public DesignerModeStore(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime;
        }

        public DesignerMode Mode { get; private set; } = DesignerMode.Standard;

        public bool IsAdvanced => this.Mode == DesignerMode.Advanced;

        /// <summary>Loads the persisted mode. Returns true when the loaded value differs from the
        /// current one, so callers know a re-render is needed. Must run after the first render
        /// (JS interop is unavailable during prerender).</summary>
        public async Task<bool> TryRestoreAsync()
        {
            string? stored;

            try
            {
                stored = await this.jsRuntime.InvokeAsync<string?>("reportBuilder.loadDraft", StorageKey);
            }
            catch (JSException)
            {
                return false;
            }

            if (!Enum.TryParse<DesignerMode>(stored, ignoreCase: true, out var mode) || mode == this.Mode)
            {
                return false;
            }

            this.Mode = mode;
            return true;
        }

        public async Task SetModeAsync(DesignerMode mode)
        {
            this.Mode = mode;

            try
            {
                await this.jsRuntime.InvokeVoidAsync("reportBuilder.saveDraft", StorageKey, mode.ToString());
            }
            catch (JSException)
            {
                // Persistence is best-effort — the in-circuit mode is already switched.
            }
        }
    }
}
