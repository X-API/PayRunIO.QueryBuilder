namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    using Microsoft.Extensions.Configuration;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    ///     Service responsible for building an OpenAI‑style chat completion request as raw JSON.
    /// </summary>
    public interface IRequestBuilderService
    {
        /// <summary>
        ///     Creates a JSON string representing the request payload expected by the OpenAI Chat Completions endpoint.
        ///     Accepts separate collections of <c>system</c> and <c>user</c> prompts.
        /// </summary>
        /// <param name="chatPrompts">The chat prompt messages.</param>
        /// <param name="model">(Optional) Override the model ID (defaults to configuration or GPT‑4o‑mini).</param>
        /// <param name="temperature">(Optional) Sampling temperature (defaults to configuration or 0.7).</param>
        /// <returns>JSON string.</returns>
        string CreateAiRequestJson(
            ChatMessage[] chatPrompts,
            string? model = null,
            double? temperature = null);
    }

    /// <inheritdoc />
    internal sealed class RequestBuilderService : IRequestBuilderService
    {
        /// <summary>
        /// The default model.
        /// </summary>
        private readonly string defaultModel;

        /// <summary>
        /// The default temperature.
        /// </summary>
        private readonly double defaultTemperature;

        /// <summary>
        /// The json serializer options.
        /// </summary>
        private readonly JsonSerializerOptions jsonSerializerOptions =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestBuilderService"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public RequestBuilderService(IConfiguration configuration)
        {
            var configuration1 = configuration ?? throw new ArgumentNullException(nameof(configuration));

            this.defaultModel = configuration1["OpenAI:Model"] ?? "gpt-4o-mini";
            var temperatureAsString = configuration1["OpenAI:Temperature"];
            this.defaultTemperature = double.TryParse(temperatureAsString, out var t) ? t : 0.7;
        }

        /// <inheritdoc />
        public string CreateAiRequestJson(
            ChatMessage[] chatPrompts,
            string? model = null,
            double? temperature = null)
        {
            if (chatPrompts is null || chatPrompts.Length == 0)
            {
                throw new ArgumentException("At least one user prompt must be provided.", nameof(chatPrompts));
            }

            var messages = new List<object>();

            var systemPrompts = chatPrompts.Where(p => p.Role == ParticipantType.System).ToArray();

            // Add system messages first.
            foreach (var s in systemPrompts)
            {
                messages.Add(new { role = "system", content = s.Text });
            }

            // Add chat messages.
            foreach (var u in chatPrompts.Where(p => p.Role != ParticipantType.System))
            {
                messages.Add(new { role = u.Role.ToString().ToLower(), content = u.Text });
            }

            var requestPayload = new
            {
                model = model ?? this.defaultModel,
                messages = messages.ToArray(),
                temperature = temperature ?? this.defaultTemperature,
            };

            return JsonSerializer.Serialize(requestPayload, this.jsonSerializerOptions);
        }
    }
}
