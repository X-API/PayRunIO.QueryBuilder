namespace PayRunIO.ReportBuilder.Logging
{
    using System.Security.Claims;

    /// <summary>
    /// Per-circuit diagnostic identity. Blazor Server holds a circuit open across many requests, so
    /// the signed in user cannot be read from an ambient HttpContext at the point a query fails.
    /// This scoped service captures it once when the designer loads and stamps every subsequent log
    /// event, letting a failed query in BetterStack be traced back to a user and a session.
    /// </summary>
    public sealed class DiagnosticContext
    {
        /// <summary>
        /// Identifies all log events raised by one browser session. Flattened to a top level
        /// "correlationId" field by the BetterStack appender, so a whole session's failures can be
        /// pulled up from any one of them.
        /// </summary>
        public string CorrelationId { get; } = Guid.NewGuid().ToString("N")[..12];

        /// <summary>
        /// The KeyCloak subject claim of the signed in user, or null before sign in completes.
        /// The subject rather than the username: it identifies the account without recording a
        /// person's name or email address in an external log store.
        /// </summary>
        public string? UserSubject { get; private set; }

        public void Capture(ClaimsPrincipal? principal) =>
            this.UserSubject = principal?.FindFirst("sub")?.Value
                               ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
