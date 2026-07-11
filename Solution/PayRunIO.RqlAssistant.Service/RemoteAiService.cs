namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;

    using PayRunIO.RqlAssistant.Service.Models;
    using PayRunIO.RqlAssistant.Service.Wire;

    /// <summary>
    /// Abstraction for calling the configured provider's chat endpoint.
    /// </summary>
    public interface IRemoteAiService
    {
        /// <summary>
        /// Sends the JSON chat completion request and returns the assistant's first reply text.
        /// Use <see cref="GetChatResponseAsync"/> when tool-calling is enabled so tool_calls are surfaced.
        /// </summary>
        Task<string> GetResponseAsync(string promptJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the JSON chat completion request and returns the structured assistant reply,
        /// preserving either final <see cref="OpenAiChatResponse.Content"/> or pending tool calls.
        /// </summary>
        Task<OpenAiChatResponse> GetChatResponseAsync(string promptJson, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Exception thrown when the AI service returns an error or an unexpected wire‑format.
    /// </summary>
    public sealed class OpenAiException : Exception
    {
        public OpenAiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
            : base(message, inner) => this.StatusCode = statusCode;

        public HttpStatusCode? StatusCode { get; }
    }

    /// <summary>
    /// Default implementation that sends a JSON request to the configured provider's chat completions
    /// endpoint (via <see cref="IChatWireFormat"/>) and returns the assistant reply.
    /// </summary>
    internal sealed class RemoteAiService : IRemoteAiService
    {
        private const string DefaultHost = "https://api.openai.com";

        private readonly HttpClient httpClient;

        private readonly IChatWireFormat wireFormat;

        private readonly string endpoint;

        /// <summary>
        /// Set once the provider rejects the 'temperature' parameter (reasoning models accept no
        /// sampling parameters). Subsequent requests in this service's lifetime strip it up front
        /// instead of paying a failed round trip every call.
        /// </summary>
        private bool temperatureUnsupported;

        public RemoteAiService(IConfiguration configuration, HttpClient httpClient, IChatWireFormat wireFormat)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.wireFormat = wireFormat ?? throw new ArgumentNullException(nameof(wireFormat));
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("Missing configuration value 'OpenAI:ApiKey'.");

            var host = configuration["OpenAI:Endpoint"];
            this.endpoint = this.wireFormat.BuildRequestUrl(string.IsNullOrWhiteSpace(host) ? DefaultHost : host);

            // Configure the HttpClient once. We *do not* dispose it here – DI owns its lifetime.
            this.wireFormat.ApplyAuthHeaders(this.httpClient, apiKey);
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <inheritdoc />
        public async Task<string> GetResponseAsync(string promptJson, CancellationToken cancellationToken = default)
        {
            var response = await this.GetChatResponseAsync(promptJson, cancellationToken).ConfigureAwait(false);

            if (response.HasToolCalls && string.IsNullOrEmpty(response.Content))
            {
                throw new OpenAiException(
                    "The provider returned tool_calls but the legacy GetResponseAsync(string) overload expects a final content reply. Use GetChatResponseAsync.");
            }

            return response.Content ?? string.Empty;
        }

        /// <inheritdoc />
        public async Task<OpenAiChatResponse> GetChatResponseAsync(string promptJson, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(promptJson))
            {
                throw new ArgumentException("Prompt JSON payload cannot be null or empty.", nameof(promptJson));
            }

            try
            {
                var payload = this.temperatureUnsupported ? RemoveTemperature(promptJson) : promptJson;

                var (statusCode, responseBody, isSuccess) = await this.PostAsync(payload, cancellationToken).ConfigureAwait(false);

                if (!isSuccess)
                {
                    var errorMessage = this.wireFormat.ExtractErrorMessage(responseBody) ?? "Unknown error";

                    // Reasoning models reject sampling parameters, and which models do isn't knowable
                    // from configuration — adapt on first rejection and remember for this session.
                    if (!this.temperatureUnsupported && IsTemperatureUnsupportedError(errorMessage))
                    {
                        this.temperatureUnsupported = true;

                        (statusCode, responseBody, isSuccess) = await this
                            .PostAsync(RemoveTemperature(promptJson), cancellationToken)
                            .ConfigureAwait(false);

                        if (!isSuccess)
                        {
                            errorMessage = this.wireFormat.ExtractErrorMessage(responseBody) ?? "Unknown error";
                            throw new OpenAiException(errorMessage, statusCode);
                        }
                    }
                    else
                    {
                        throw new OpenAiException(errorMessage, statusCode);
                    }
                }

                try
                {
                    return this.wireFormat.ParseResponse(responseBody, statusCode);
                }
                catch (JsonException jex)
                {
                    throw new OpenAiException("Failed to parse the provider's response JSON.", statusCode, jex);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // propagate cancellation to caller
            }
            catch (HttpRequestException hex)
            {
                throw new OpenAiException("HTTP request to the AI provider failed.", null, hex);
            }
        }

        private async Task<(HttpStatusCode StatusCode, string Body, bool IsSuccess)> PostAsync(
            string payload,
            CancellationToken cancellationToken)
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await this.httpClient
                .PostAsync(this.endpoint, content, cancellationToken)
                .ConfigureAwait(false);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return (response.StatusCode, responseBody, response.IsSuccessStatusCode);
        }

        private static bool IsTemperatureUnsupportedError(string? errorMessage) =>
            errorMessage != null
            && errorMessage.Contains("temperature", StringComparison.OrdinalIgnoreCase)
            && (errorMessage.Contains("not supported", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("unsupported", StringComparison.OrdinalIgnoreCase));

        private static string RemoveTemperature(string promptJson)
        {
            try
            {
                var root = System.Text.Json.Nodes.JsonNode.Parse(promptJson);

                if (root is System.Text.Json.Nodes.JsonObject payload && payload.Remove("temperature"))
                {
                    return root.ToJsonString();
                }
            }
            catch (JsonException)
            {
                // Malformed payloads surface as provider errors on the original request instead.
            }

            return promptJson;
        }
    }
}
