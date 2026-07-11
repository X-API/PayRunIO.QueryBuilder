namespace PayRunIO.RqlAssistant.Service.Wire
{
    using System;

    /// <summary>
    /// Builds a provider request URL from a user-supplied endpoint that may already carry an API
    /// path. Users paste full URLs for whichever API they last used (e.g.
    /// "https://api.openai.com/v1/responses"), so any recognised provider path is stripped before
    /// the selected wire format's own path is appended — the configured provider always decides the
    /// path, never the stale endpoint suffix. Unrecognised paths (custom proxies) are preserved.
    /// </summary>
    internal static class ApiPathNormaliser
    {
        private static readonly string[] KnownSuffixes =
            {
                "/v1/chat/completions",
                "/v1/messages",
                "/v1/responses"
            };

        public static string BuildUrl(string host, string pathSuffix)
        {
            var trimmed = host.TrimEnd('/');

            if (trimmed.EndsWith(pathSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            foreach (var suffix in KnownSuffixes)
            {
                if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed[..^suffix.Length].TrimEnd('/');
                    break;
                }
            }

            return trimmed + pathSuffix;
        }
    }
}
