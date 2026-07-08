namespace PayRunIO.RqlAssistant.Service.Wire
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;

    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Encapsulates the provider-specific wire contract for a chat completions style API (request shape,
    /// URL, auth headers, response/error parsing). <see cref="RequestBuilderService"/> and
    /// <see cref="RemoteAiService"/> delegate to an implementation of this interface rather than assuming
    /// any single provider's JSON shape, so the rest of the RAG pipeline (<see cref="ChatMessage"/>,
    /// <see cref="ToolDescriptor"/>, <see cref="OpenAiChatResponse"/>) stays provider-agnostic.
    /// </summary>
    public interface IChatWireFormat
    {
        /// <summary>
        /// Builds the provider-specific JSON request payload for the given chat history and optional tools.
        /// </summary>
        string BuildRequestJson(
            ChatMessage[] chatPrompts,
            IReadOnlyList<ToolDescriptor>? tools,
            string model,
            double temperature);

        /// <summary>
        /// Builds the full request URL for this provider from a user-supplied host (e.g. "https://api.openai.com").
        /// </summary>
        string BuildRequestUrl(string host);

        /// <summary>
        /// Applies this provider's authentication scheme to the shared <see cref="HttpClient"/>.
        /// </summary>
        void ApplyAuthHeaders(HttpClient httpClient, string apiKey);

        /// <summary>
        /// Parses a successful response body into the provider-agnostic <see cref="OpenAiChatResponse"/> shape.
        /// </summary>
        OpenAiChatResponse ParseResponse(string responseBody, HttpStatusCode statusCode);

        /// <summary>
        /// Extracts a human-readable error message from an error response body, or <c>null</c> if none could
        /// be extracted (in which case the caller falls back to the raw body / reason phrase).
        /// </summary>
        string? ExtractErrorMessage(string responseBody);
    }
}
