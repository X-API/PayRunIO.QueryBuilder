namespace PayRunIO.ReportBuilder.Auth
{
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Server side store of KeyCloak tokens keyed by the user's subject claim. Tokens are kept out
    /// of the auth cookie so the cookie stays small and refreshed tokens are visible to long-lived
    /// Blazor circuits.
    /// </summary>
    public interface IUserTokenStore
    {
        void Save(string subject, UserTokens tokens);

        bool TryGet(string subject, [NotNullWhen(true)] out UserTokens? tokens);

        void Remove(string subject);
    }

    /// <summary>
    /// In-memory implementation. Entries are lost on application restart, in which case users are
    /// prompted to sign in again (their auth cookie alone is not enough to call the API).
    /// </summary>
    public sealed class InMemoryUserTokenStore : IUserTokenStore
    {
        private readonly ConcurrentDictionary<string, UserTokens> tokensBySubject = new();

        public void Save(string subject, UserTokens tokens) => this.tokensBySubject[subject] = tokens;

        public bool TryGet(string subject, [NotNullWhen(true)] out UserTokens? tokens) =>
            this.tokensBySubject.TryGetValue(subject, out tokens);

        public void Remove(string subject) => this.tokensBySubject.TryRemove(subject, out _);
    }
}
