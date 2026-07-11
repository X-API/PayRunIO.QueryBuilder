namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Linq;

    /// <summary>
    /// The AI backend a configured connection targets. Drives which <see cref="Wire.IChatWireFormat"/>
    /// implementation <see cref="ServiceFactory"/> selects.
    /// </summary>
    public enum ProviderType
    {
        /// <summary>OpenAI Chat Completions endpoint (/v1/chat/completions).</summary>
        OpenAi,

        /// <summary>Anthropic Messages endpoint (/v1/messages).</summary>
        Anthropic,

        /// <summary>OpenAI Responses endpoint (/v1/responses) — required for function tools on
        /// reasoning models (GPT-5 family) unless reasoning is disabled.</summary>
        OpenAiResponses
    }

    public static class ProviderTypeParser
    {
        /// <summary>
        /// Parses a provider name (as persisted in settings/configuration) to a <see cref="ProviderType"/>,
        /// falling back to <see cref="ProviderType.OpenAi"/> for null, empty, or unrecognized values so
        /// existing single-provider configurations keep working unmodified. Accepts punctuation/spacing
        /// variants such as "OpenAI (Responses)", "OpenAI Responses" or "Responses".
        /// </summary>
        public static ProviderType ParseOrDefault(string? providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                return ProviderType.OpenAi;
            }

            var normalised = new string(providerName.Where(char.IsLetter).ToArray());

            if (string.Equals(normalised, "Anthropic", StringComparison.OrdinalIgnoreCase))
            {
                return ProviderType.Anthropic;
            }

            if (string.Equals(normalised, "OpenAIResponses", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalised, "Responses", StringComparison.OrdinalIgnoreCase))
            {
                return ProviderType.OpenAiResponses;
            }

            return ProviderType.OpenAi;
        }
    }
}
