namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    using Microsoft.Extensions.Configuration;
    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    ///     Service responsible for building an OpenAI‑style chat completion request as raw JSON.
    /// </summary>
    public interface IRequestBuilderService
    {
        /// <summary>
        /// Creates a JSON string representing the request payload expected by the OpenAI Chat Completions endpoint.
        /// </summary>
        /// <param name="chatPrompts">The full chat message history, in order. System messages are emitted first; all
        /// other roles preserve their position so tool-call/tool-result pairing is intact.</param>
        /// <param name="tools">Optional tool descriptors. When provided, the request includes a <c>tools</c> array and
        /// <c>tool_choice:"auto"</c> so the model may emit <c>tool_calls</c> instead of (or before) a final reply.</param>
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
        private readonly string defaultModel;

        private readonly double defaultTemperature;

        private readonly JsonSerializerOptions jsonSerializerOptions =
            new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

        public RequestBuilderService(IConfiguration configuration)
        {
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

            var messages = new List<object>();

            // Emit system messages first so they remain pinned at the top of the conversation.
            foreach (var s in chatPrompts.Where(p => p.Role == ParticipantType.System))
            {
                messages.Add(new { role = "system", content = s.Text });
            }

            // Other roles keep their relative order so assistant tool_calls stay paired with their tool replies.
            foreach (var m in chatPrompts.Where(p => p.Role != ParticipantType.System))
            {
                messages.Add(ToWireMessage(m));
            }

            object requestPayload;

            if (tools != null && tools.Count > 0)
            {
                requestPayload = new
                    {
                        model = model ?? this.defaultModel,
                        messages = messages.ToArray(),
                        temperature = temperature ?? this.defaultTemperature,
                        tools = tools.Select(ToWireTool).ToArray(),
                        tool_choice = "auto"
                    };
            }
            else
            {
                requestPayload = new
                    {
                        model = model ?? this.defaultModel,
                        messages = messages.ToArray(),
                        temperature = temperature ?? this.defaultTemperature
                    };
            }

            return JsonSerializer.Serialize(requestPayload, this.jsonSerializerOptions);
        }

        private static object ToWireMessage(ChatMessage message)
        {
            switch (message.Role)
            {
                case ParticipantType.Assistant when message.ToolCalls is { Count: > 0 }:
                    return new
                        {
                            role = "assistant",
                            content = message.Text ?? string.Empty,
                            tool_calls = message.ToolCalls.Select(tc => new
                                {
                                    id = tc.Id,
                                    type = "function",
                                    function = new
                                        {
                                            name = tc.FunctionName,
                                            arguments = tc.ArgumentsJson ?? "{}"
                                        }
                                }).ToArray()
                        };

                case ParticipantType.Tool:
                    return new
                        {
                            role = "tool",
                            tool_call_id = message.ToolCallId ?? string.Empty,
                            content = message.Text ?? string.Empty
                        };

                default:
                    return new { role = message.Role.ToString().ToLower(), content = message.Text };
            }
        }

        private static object ToWireTool(ToolDescriptor descriptor)
        {
            // Parameters are pre-serialised JSON Schema fragments — parse them into a JsonElement so the
            // outer serializer inlines the structure rather than emitting it as a quoted string.
            using var doc = JsonDocument.Parse(descriptor.ParametersJsonSchema);

            return new
                {
                    type = "function",
                    function = new
                        {
                            name = descriptor.Name,
                            description = descriptor.Description,
                            parameters = doc.RootElement.Clone()
                        }
                };
        }
    }
}
