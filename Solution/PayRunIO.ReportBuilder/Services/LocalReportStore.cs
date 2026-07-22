namespace PayRunIO.ReportBuilder.Services
{
    using System.Text.Json;

    using Microsoft.JSInterop;

    /// <summary>A report saved in the user's own browser storage.</summary>
    public sealed class LocalReport
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string QueryXml { get; set; } = string.Empty;

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }

        /// <summary>API identifier of the report definition this local report was last published to,
        /// or null when it has never been published. Kept on the local copy so re-publishing updates
        /// the same definition instead of creating a duplicate.</summary>
        public string? PublishedReportId { get; set; }
    }

    /// <summary>
    /// The user's private collection of saved reports, held in browser localStorage rather than in the
    /// PayRun.io account. Report definitions stored via the API are visible to everyone, so ordinary
    /// saving is deliberately local-only and private to this browser profile; pushing a report to the
    /// shared API surface is the separate, explicit "publish" step owned by
    /// <see cref="ReportDefinitionService"/>.
    /// </summary>
    public sealed class LocalReportStore
    {
        private const string StorageKey = "payrun.reportbuilder.reports.v1";

        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly IJSRuntime jsRuntime;

        public LocalReportStore(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime;
        }

        /// <summary>Lists the saved reports, most recently updated first. Must run after the first
        /// render — JS interop is unavailable during prerender.</summary>
        public async Task<IReadOnlyList<LocalReport>> ListAsync()
        {
            var reports = await this.ReadAllAsync();

            return reports
                .OrderByDescending(report => report.UpdatedUtc)
                .ToList();
        }

        public async Task<LocalReport?> LoadAsync(string id)
        {
            var reports = await this.ReadAllAsync();

            return reports.FirstOrDefault(report => string.Equals(report.Id, id, StringComparison.Ordinal));
        }

        /// <summary>Creates or updates a report and returns the stored record. A null or unknown
        /// <paramref name="id"/> creates a new entry, so the caller can pass the session's current id
        /// without first checking whether it still exists.</summary>
        public async Task<LocalReport> SaveAsync(string? id, string name, string queryXml, string? publishedReportId = null)
        {
            var reports = await this.ReadAllAsync();

            var existing = id == null
                               ? null
                               : reports.FirstOrDefault(report => string.Equals(report.Id, id, StringComparison.Ordinal));

            var now = DateTimeOffset.UtcNow;

            if (existing == null)
            {
                existing = new LocalReport { Id = Guid.NewGuid().ToString("N"), CreatedUtc = now };
                reports.Add(existing);
            }

            existing.Name = name;
            existing.QueryXml = queryXml;
            existing.UpdatedUtc = now;

            // Only overwrite the published link when the caller supplies one — a plain local save of a
            // previously published report must not sever the link to its published definition.
            if (publishedReportId != null)
            {
                existing.PublishedReportId = publishedReportId;
            }

            await this.WriteAllAsync(reports);

            return existing;
        }

        public async Task DeleteAsync(string id)
        {
            var reports = await this.ReadAllAsync();

            if (reports.RemoveAll(report => string.Equals(report.Id, id, StringComparison.Ordinal)) > 0)
            {
                await this.WriteAllAsync(reports);
            }
        }

        private async Task<List<LocalReport>> ReadAllAsync()
        {
            string? json;

            try
            {
                json = await this.jsRuntime.InvokeAsync<string?>("reportBuilder.loadDraft", StorageKey);
            }
            catch (JSException)
            {
                return new List<LocalReport>();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<LocalReport>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<LocalReport>>(json, SerializerOptions) ?? new List<LocalReport>();
            }
            catch (JsonException)
            {
                // Corrupt or schema-changed collection. Unlike the draft this is the user's saved work,
                // so it is left in place rather than cleared — a future schema can still migrate it.
                return new List<LocalReport>();
            }
        }

        private async Task WriteAllAsync(List<LocalReport> reports)
        {
            var json = JsonSerializer.Serialize(reports, SerializerOptions);

            try
            {
                await this.jsRuntime.InvokeVoidAsync("reportBuilder.saveDraft", StorageKey, json);
            }
            catch (JSException)
            {
                // Best-effort, as elsewhere — storage may be unavailable or over quota.
            }
        }
    }
}
