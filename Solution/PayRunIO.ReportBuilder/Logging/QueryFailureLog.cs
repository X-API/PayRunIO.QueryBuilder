namespace PayRunIO.ReportBuilder.Logging
{
    using log4net;
    using log4net.Core;

    /// <summary>
    /// Records report query failures in a consistent, queryable shape.
    ///
    /// The purpose is to build an evidence base for improving the RQL assistant: every entry pairs
    /// the exact query text with the reason it failed, so recurring failure modes can be found in
    /// BetterStack and fed back into the grammar, linter rules and example set. The full query XML
    /// is recorded deliberately — a failure that cannot be replayed cannot be fixed.
    /// </summary>
    public sealed class QueryFailureLog
    {
        /// <summary>
        /// Named rather than typed (<c>ILog&lt;T&gt;</c> has no equivalent) so every failure event
        /// lands under the one logger the BetterStack appender is attached to in log4net.config.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger("PayRunIO.ReportBuilder.QueryFailures");

        private readonly DiagnosticContext diagnostics;

        public QueryFailureLog(DiagnosticContext diagnostics) => this.diagnostics = diagnostics;

        /// <summary>
        /// Records a query the PayRun.io API rejected. These are the highest value entries: the
        /// query was well formed enough to send, so the failure reflects a genuine gap between what
        /// the assistant believes about the schema and what the API enforces.
        /// </summary>
        /// <param name="queryXml">The query as executed.</param>
        /// <param name="statusCode">The HTTP status returned by the API.</param>
        /// <param name="reasonPhrase">The HTTP reason phrase.</param>
        /// <param name="errorMessage">The error message extracted from the API response body.</param>
        /// <param name="origin">Whether the query was written by the assistant or edited by hand.</param>
        public void QueryRejected(
            string queryXml,
            int statusCode,
            string? reasonPhrase,
            string errorMessage,
            QueryOrigin origin)
        {
            var properties = this.BaseProperties(queryXml, origin);

            properties["statusCode"] = statusCode;
            properties["failureKind"] = "ApiRejected";

            StructuredLog.Write(
                Log,
                Level.Error,
                $"RQL query rejected by the API - {statusCode} {reasonPhrase}: {errorMessage}",
                properties);
        }

        /// <summary>
        /// Records a query that failed before or below the API contract — a transport fault, a
        /// timeout, or an unparseable response. Distinguished from a rejection because the query
        /// itself may be perfectly valid, and these should not skew assistant quality analysis.
        /// </summary>
        public void QueryFaulted(string queryXml, Exception exception, QueryOrigin origin)
        {
            var properties = this.BaseProperties(queryXml, origin);

            properties["failureKind"] = "Faulted";

            StructuredLog.Write(
                Log,
                Level.Error,
                $"RQL query execution faulted - {exception.GetType().Name}: {exception.Message}",
                properties,
                exception);
        }

        /// <summary>
        /// Records a successful execution that returned a response the tabular parser could not
        /// read. Not a user facing failure, but it means the report renders as raw XML instead of a
        /// table, which is worth knowing about.
        /// </summary>
        public void ResponseNotTabular(string queryXml, QueryOrigin origin)
        {
            var properties = this.BaseProperties(queryXml, origin);

            properties["failureKind"] = "NonTabularResponse";

            StructuredLog.Write(
                Log,
                Level.Warn,
                "RQL query succeeded but the response could not be parsed as a table.",
                properties);
        }

        private Dictionary<string, object?> BaseProperties(string queryXml, QueryOrigin origin) =>
            new()
                {
                    ["correlationId"] = this.diagnostics.CorrelationId,
                    ["userSubject"] = this.diagnostics.UserSubject,
                    ["prioIdentity"] = this.diagnostics.PrioIdentity,
                    ["queryXml"] = queryXml,
                    ["queryOrigin"] = origin.ToString(),
                };
    }

    /// <summary>
    /// Where the executed query came from. Separating assistant generated queries from hand edited
    /// ones is what makes the log usable for measuring assistant quality — a failure the user typed
    /// themselves says nothing about the model.
    /// </summary>
    public enum QueryOrigin
    {
        /// <summary>The origin could not be determined.</summary>
        Unknown,

        /// <summary>The query is exactly as the assistant last produced it.</summary>
        Assistant,

        /// <summary>The query was edited by the user after the assistant produced it, or written from scratch.</summary>
        UserEdited,

        /// <summary>The query came from the built in report catalog or a saved report definition.</summary>
        Stored,
    }
}
