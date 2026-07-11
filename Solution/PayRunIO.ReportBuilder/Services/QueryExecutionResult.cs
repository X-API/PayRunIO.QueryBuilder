namespace PayRunIO.ReportBuilder.Services
{
    public sealed class QueryExecutionResult
    {
        public bool Success { get; init; }

        public string? ErrorMessage { get; init; }

        public string RawXml { get; init; } = string.Empty;

        /// <summary>
        /// Populated when the response follows the tabular output pattern; null for any other
        /// response shape (the raw XML is still available for display/download).
        /// </summary>
        public TabularResult? Table { get; init; }
    }
}
