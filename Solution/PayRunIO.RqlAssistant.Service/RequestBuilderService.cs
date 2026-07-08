namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Extensions.Configuration;
    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;
    using PayRunIO.RqlAssistant.Service.Wire;

    /// <summary>
    ///     Service responsible for building a provider-specific chat completion request as raw JSON.
    /// </summary>
    public interface IRequestBuilderService
    {
        /// <summary>
        /// Creates a JSON string representing the request payload expected by the configured provider's
        /// chat completions endpoint.
        /// </summary>
        /// <param name="chatPrompts">The full chat message history, in order. System messages are emitted first (or
        /// hoisted to a top-level field, depending on the provider); all other roles preserve their position so
        /// tool-call/tool-result pairing is intact.</param>
        /// <param name="tools">Optional tool descriptors. When provided, the request includes provider-specific
        /// tool definitions so the model may request tool invocations instead of (or before) a final reply.</param>
        /// <param name="model">(Optional) Override the model ID (defaults to configuration or GPT‑4o‑mini).</param>
        /// <param name="temperature">(Optional) Sampling temperature (defaults to configuration or 0.2).</param>
        /// <returns>JSON string suitable for posting to the chat completions endpoint.</returns>
        string CreateAiRequestJson(
            ChatMessage[] chatPrompts,
            IReadOnlyList<ToolDescriptor>? tools = null,
            string? model = null,
            double? temperature = null);
    }

    /// <inheritdoc />
    internal sealed class RequestBuilderService : IRequestBuilderService
    {
        private readonly IChatWireFormat wireFormat;

        private readonly string defaultModel;

        private readonly double defaultTemperature;

        public RequestBuilderService(IConfiguration configuration, IChatWireFormat wireFormat)
        {
            this.wireFormat = wireFormat ?? throw new ArgumentNullException(nameof(wireFormat));

            var configuration1 = configuration ?? throw new ArgumentNullException(nameof(configuration));

            this.defaultModel = configuration1["OpenAI:Model"] ?? "gpt-4o-mini";
            var temperatureAsString = configuration1["OpenAI:Temperature"];

            // DSL generation wants near-deterministic sampling; higher temperatures measurably
            // increase schema-validation failures.
            this.defaultTemperature = double.TryParse(temperatureAsString, out var t) ? t : 0.2;
        }

        /// <inheritdoc />
        public string CreateAiRequestJson(
            ChatMessage[] chatPrompts,
            IReadOnlyList<ToolDescriptor>? tools = null,
            string? model = null,
            double? temperature = null)
        {
            if (chatPrompts is null || chatPrompts.Length == 0)
            {
                throw new ArgumentException("At least one user prompt must be provided.", nameof(chatPrompts));
            }

            return this.wireFormat.BuildRequestJson(
                chatPrompts,
                tools,
                model ?? this.defaultModel,
                temperature ?? this.defaultTemperature);
        }
    }
}
