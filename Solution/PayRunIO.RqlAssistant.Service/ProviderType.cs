namespace PayRunIO.RqlAssistant.Service
{
    using System;

    /// <summary>
    /// The AI backend a configured connection targets. Drives which <see cref="Wire.IChatWireFormat"/>
    /// implementation <see cref="ServiceFactory"/> selects.
    /// </summary>
    public enum ProviderType
    {
        OpenAi,

        Anthropic
    }

    public static class ProviderTypeParser
    {
        /// <summary>
        /// Parses a provider name (as persisted in settings/configuration) to a <see cref="ProviderType"/>,
        /// falling back to <see cref="ProviderType.OpenAi"/> for null, empty, or unrecognized values so
        /// existing single-provider configurations keep working unmodified.
        /// </summary>
        public static ProviderType ParseOrDefault(string? providerName)
        {
            if (string.Equals(providerName, "Anthropic", StringComparison.OrdinalIgnoreCase))
            {
                return ProviderType.Anthropic;
            }

            return ProviderType.OpenAi;
        }
    }
}
