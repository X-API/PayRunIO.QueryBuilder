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

    /// <summary>
    /// Abstraction for calling the OpenAI chat endpoint.
    /// </summary>
    public interface IRemoteAiService
    {
        /// <summary>
        /// Sends the JSON chat completion request to OpenAI and returns the assistant's first reply.
        /// </summary>
        /// <param name="promptJson">
        /// The prompt Json.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation Token.
        /// </param>
        /// <returns>
        /// The <see cref="string" /> <see cref="Task"/>.
        /// </returns>
        Task<string> GetResponseAsync(string promptJson, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Exception thrown when the OpenAI service returns an error or an unexpected wire‑format.
    /// </summary>
    public sealed class OpenAiException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAiException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="statusCode">
        /// The status code.
        /// </param>
        /// <param name="inner">
        /// The inner.
        /// </param>
        public OpenAiException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
            : base(message, inner) => this.StatusCode = statusCode;

        /// <summary>
        /// Gets the status code.
        /// </summary>
        public HttpStatusCode? StatusCode { get; }
    }

    /// <summary>
    /// Default implementation that sends a JSON request to the OpenAI Chat Completions endpoint and returns the assistant reply.
    /// </summary>
    internal sealed class RemoteAiService : IRemoteAiService
    {
        /// <summary>
        /// The default endpoint.
        /// </summary>
        private const string DefaultEndpoint = "https://api.openai.com/v1/chat/completions";

        /// <summary>
        /// The http client.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// The endpoint.
        /// </summary>
        private readonly string endpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteAiService"/> class. 
        /// </summary>
        /// <param name="configuration">
        /// Application configuration containing <c>OpenAI:ApiKey</c> and optional <c>OpenAI:Endpoint</c>.
        /// </param>
        /// <param name="httpClient">
        /// A pre‑configured <see cref="HttpClient"/> (registered via DI).
        /// </param>
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
            if (string.IsNullOrWhiteSpace(promptJson))
            {
                throw new ArgumentException("Prompt JSON payload cannot be null or empty.", nameof(promptJson));
            }

            try
            {
                using (var content = new StringContent(promptJson, Encoding.UTF8, "application/json"))
                {
                    using (var response = await this.httpClient.PostAsync(this.endpoint, content, cancellationToken)
                                              .ConfigureAwait(false))
                    {
                        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorMessage = ExtractOpenAiError(responseBody) ?? response.ReasonPhrase ?? "Unknown error";
                            throw new OpenAiException(errorMessage, response.StatusCode);
                        }

                        try
                        {
                            using (var doc = JsonDocument.Parse(responseBody))
                            {
                                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                                {
                                    var contentText = choices[0].GetProperty("message").GetProperty("content").GetString();
                                    return contentText ?? string.Empty;
                                }
                            }

                            throw new OpenAiException("Response JSON missing 'choices[0].message.content'.", response.StatusCode);
                        }
                        catch (JsonException jex)
                        {
                            throw new OpenAiException("Failed to parse OpenAI response JSON.", response.StatusCode, jex);
                        }
                    }
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

        /// <summary>
        /// The extract open AI error method.
        /// </summary>
        /// <param name="responseBody">The response body.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string? ExtractOpenAiError(string responseBody)
        {
            try
            {
                using (var doc = JsonDocument.Parse(responseBody))
                {
                    if (doc.RootElement.TryGetProperty("error", out var error))
                    {
                        if (error.ValueKind == JsonValueKind.String)
                        {
                            return error.GetRawText();
                        }

                        return error.GetProperty("message").GetString();
                    }
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
