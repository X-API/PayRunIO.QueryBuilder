namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;

    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Abstraction for calling the OpenAI chat endpoint.
    /// </summary>
    public interface IRemoteAiService
    {
        /// <summary>
        /// Sends the JSON chat completion request to OpenAI and returns the assistant's first reply text.
        /// Use <see cref="GetChatResponseAsync"/> when tool-calling is enabled so tool_calls are surfaced.
        /// </summary>
        Task<string> GetResponseAsync(string promptJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the JSON chat completion request to OpenAI and returns the structured assistant reply,
        /// preserving either final <see cref="OpenAiChatResponse.Content"/> or pending tool calls.
        /// </summary>
        Task<OpenAiChatResponse> GetChatResponseAsync(string promptJson, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Exception thrown when the OpenAI service returns an error or an unexpected wire‑format.
    /// </summary>
    public sealed class OpenAiException : Exception
    {
        public OpenAiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
            : base(message, inner) => this.StatusCode = statusCode;

        public HttpStatusCode? StatusCode { get; }
    }

    /// <summary>
    /// Default implementation that sends a JSON request to the OpenAI Chat Completions endpoint and returns the assistant reply.
    /// </summary>
    internal sealed class RemoteAiService : IRemoteAiService
    {
        private const string DefaultEndpoint = "https://api.openai.com/v1/chat/completions";

        private readonly HttpClient httpClient;

        private readonly string endpoint;

        public RemoteAiService(IConfiguration configuration, HttpClient httpClient)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("Missing configuration value 'OpenAI:ApiKey'.");

            this.endpoint = configuration["OpenAI:Endpoint"] ?? DefaultEndpoint;

            // Configure the HttpClient once. We *do not* dispose it here – DI owns its lifetime.
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <inheritdoc />
        public async Task<string> GetResponseAsync(string promptJson, CancellationToken cancellationToken = default)
        {
            var response = await this.GetChatResponseAsync(promptJson, cancellationToken).ConfigureAwait(false);

            if (response.HasToolCalls && string.IsNullOrEmpty(response.Content))
            {
                throw new OpenAiException(
                    "OpenAI returned tool_calls but the legacy GetResponseAsync(string) overload expects a final content reply. Use GetChatResponseAsync.");
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
                using var content = new StringContent(promptJson, Encoding.UTF8, "application/json");
                using var response = await this.httpClient
                    .PostAsync(this.endpoint, content, cancellationToken)
                    .ConfigureAwait(false);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = ExtractOpenAiError(responseBody) ?? response.ReasonPhrase ?? "Unknown error";
                    throw new OpenAiException(errorMessage, response.StatusCode);
                }

                try
                {
                    return ParseChatResponse(responseBody, response.StatusCode);
                }
                catch (JsonException jex)
                {
                    throw new OpenAiException("Failed to parse OpenAI response JSON.", response.StatusCode, jex);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // propagate cancellation to caller
            }
            catch (HttpRequestException hex)
            {
                throw new OpenAiException("HTTP request to OpenAI failed.", null, hex);
            }
        }

        private static OpenAiChatResponse ParseChatResponse(string responseBody, HttpStatusCode statusCode)
        {
            using var doc = JsonDocument.Parse(responseBody);

            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                throw new OpenAiException("Response JSON missing 'choices[0].message.content'.", statusCode);
            }

            var message = choices[0].GetProperty("message");

            string? content = null;
            if (message.TryGetProperty("content", out var contentElement)
                && contentElement.ValueKind == JsonValueKind.String)
            {
                content = contentElement.GetString();
            }

            var toolCalls = new List<OpenAiToolCall>();
            if (message.TryGetProperty("tool_calls", out var toolCallsElement)
                && toolCallsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var call in toolCallsElement.EnumerateArray())
                {
                    var id = call.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                                 ? idElement.GetString() ?? string.Empty
                                 : string.Empty;

                    if (!call.TryGetProperty("function", out var function))
                    {
                        continue;
                    }

                    var name = function.TryGetProperty("name", out var nameElement)
                               && nameElement.ValueKind == JsonValueKind.String
                                   ? nameElement.GetString() ?? string.Empty
                                   : string.Empty;

                    var arguments = function.TryGetProperty("arguments", out var argsElement)
                                    && argsElement.ValueKind == JsonValueKind.String
                                        ? argsElement.GetString() ?? "{}"
                                        : "{}";

                    toolCalls.Add(new OpenAiToolCall(id, name, arguments));
                }
            }

            if (content == null && toolCalls.Count == 0)
            {
                throw new OpenAiException("Response JSON missing 'choices[0].message.content'.", statusCode);
            }

            return new OpenAiChatResponse
                {
                    Content = content,
                    ToolCalls = toolCalls
                };
        }

        private static string? ExtractOpenAiError(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);

                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.String)
                    {
                        return error.GetRawText();
                    }

                    return error.GetProperty("message").GetString();
                }
            }
            catch (JsonException)
            {
                // Swallow – we'll fall back to raw body
            }

            return responseBody.Length > 1024 ? responseBody.Substring(0, 1024) + "…" : responseBody;
        }
    }
}
