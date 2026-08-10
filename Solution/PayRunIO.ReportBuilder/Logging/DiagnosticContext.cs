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

        /// <summary>
        /// The authenticated user as "{issuer}~~{username}" — for example
        /// <c>https://auth.dev.payrun.io/realms/payescape~~UserNameA</c> — or null before sign in
        /// completes. Emitted on every log event as the custom "prioIdentity" property. The issuer
        /// is included because usernames are only unique within a realm, so the pair is what
        /// actually identifies an account.
        ///
        /// A custom property rather than log4net's built in <c>Identity</c> field: that field is
        /// read only and only ever populated by log4net itself on the .NET Framework, so on .NET 8
        /// it always reaches the appender empty.
        /// </summary>
        public string? PrioIdentity { get; private set; }

        public void Capture(ClaimsPrincipal? principal)
        {
            this.UserSubject = principal?.FindFirst("sub")?.Value
                               ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            this.PrioIdentity = ComposeIdentity(principal);
        }

        /// <summary>
        /// Builds the identity string. The issuer is taken from the "iss" claim where KeyCloak
        /// supplied one, falling back to the issuer recorded against the claims themselves
        /// (<see cref="Claim.Issuer"/>), which is set for every claim materialised from the token.
        /// Returns null unless both halves are known — a half formed "~~name" or "issuer~~" entry
        /// would be worse than no entry, because it looks like a real value when filtering.
        /// </summary>
        private static string? ComposeIdentity(ClaimsPrincipal? principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // NameClaimType is configured as "preferred_username" in Program.cs, so Identity.Name is
            // the KeyCloak username rather than a display name or the subject guid.
            var userName = principal.Identity.Name;

            var issuer = principal.FindFirst("iss")?.Value
                         ?? principal.FindFirst(c => c.Type == ClaimTypes.Name)?.Issuer
                         ?? principal.Claims.FirstOrDefault()?.Issuer;

            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            return $"{issuer}~~{userName}";
        }
    }
}
