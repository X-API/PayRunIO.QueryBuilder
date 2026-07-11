namespace PayRunIO.RqlAssistant.Service.Wire
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// OpenAI Responses API wire format (<c>/v1/responses</c>): <c>Authorization: Bearer</c> auth,
    /// <c>input[]</c> items (role messages plus typed <c>function_call</c> / <c>function_call_output</c>
    /// items), flat <c>tools[{type:"function",name,...}]</c> definitions, and an <c>output[]</c> array
    /// in replies. Required by newer reasoning models (GPT-5 family), which reject function tools on
    /// the Chat Completions endpoint unless reasoning is disabled. Conversation state is replayed in
    /// full each turn (<c>store:false</c>) so the pipeline stays stateless like the other formats.
    /// </summary>
    public sealed class OpenAiResponsesWireFormat : IChatWireFormat
    {
        private const string PathSuffix = "/v1/responses";

        private readonly string? reasoningEffort;

        private readonly JsonSerializerOptions jsonSerializerOptions =
            new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

        /// <param name="reasoningEffort">Optional reasoning effort ("none", "minimal", "low", "medium",
        /// "high"). When set, it is sent as <c>reasoning.effort</c> and <c>temperature</c> is omitted —
        /// reasoning models reject sampling parameters.</param>
        public OpenAiResponsesWireFormat(string? reasoningEffort = null)
        {
            this.reasoningEffort = string.IsNullOrWhiteSpace(reasoningEffort) ? null : reasoningEffort.Trim();
        }

        /// <inheritdoc />
        public string BuildRequestJson(
            ChatMessage[] chatPrompts,
            IReadOnlyList<ToolDescriptor>? tools,
            string model,
            double temperature)
        {
            var input = new List<object>();

            // Emit system messages first so they remain pinned at the top of the conversation.
            foreach (var s in chatPrompts.Where(p => p.Role == ParticipantType.System))
            {
                input.Add(new { role = "system", content = s.Text });
            }

            // Other roles keep their relative order so function_call items stay paired with their outputs.
            foreach (var m in chatPrompts.Where(p => p.Role != ParticipantType.System))
            {
                input.AddRange(ToWireItems(m));
            }

            var payload = new Dictionary<string, object?>
                {
                    ["model"] = model,
                    ["input"] = input.ToArray(),

                    // Full history is replayed each turn; opting out of server-side storage keeps the
                    // stateless contract explicit.
                    ["store"] = false
                };

            if (this.reasoningEffort != null)
            {
                payload["reasoning"] = new { effort = this.reasoningEffort };
            }
            else
            {
                payload["temperature"] = temperature;
            }

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = tools.Select(ToWireTool).ToArray();
                payload["tool_choice"] = "auto";
            }

            return JsonSerializer.Serialize(payload, this.jsonSerializerOptions);
        }

        /// <inheritdoc />
        public string BuildRequestUrl(string host) => ApiPathNormaliser.BuildUrl(host, PathSuffix);

        /// <inheritdoc />
        public void ApplyAuthHeaders(HttpClient httpClient, string apiKey)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <inheritdoc />
        public OpenAiChatResponse ParseResponse(string responseBody, HttpStatusCode statusCode)
        {
            using var doc = JsonDocument.Parse(responseBody);

            if (!doc.RootElement.TryGetProperty("output", out var output)
                || output.ValueKind != JsonValueKind.Array)
            {
                throw new OpenAiException("Response JSON missing 'output[]'.", statusCode);
            }

            string? content = null;
            var toolCalls = new List<OpenAiToolCall>();

            foreach (var item in output.EnumerateArray())
            {
                var itemType = item.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

                switch (itemType)
                {
                    case "message":
                        content = AppendMessageText(content, item);
                        break;

                    case "function_call":
                        // call_id (not the item id) is what a function_call_output must echo back.
                        var callId = item.TryGetProperty("call_id", out var callIdElement)
                                     && callIdElement.ValueKind == JsonValueKind.String
                                         ? callIdElement.GetString() ?? string.Empty
                                         : string.Empty;

                        var name = item.TryGetProperty("name", out var nameElement)
                                   && nameElement.ValueKind == JsonValueKind.String
                                       ? nameElement.GetString() ?? string.Empty
                                       : string.Empty;

                        var arguments = item.TryGetProperty("arguments", out var argsElement)
                                        && argsElement.ValueKind == JsonValueKind.String
                                            ? argsElement.GetString() ?? "{}"
                                            : "{}";

                        toolCalls.Add(new OpenAiToolCall(callId, name, arguments));
                        break;

                    // "reasoning" and other item types carry no conversational payload.
                }
            }

            if (content == null && toolCalls.Count == 0)
            {
                throw new OpenAiException("Response JSON contained no message or function_call output items.", statusCode);
            }

            return new OpenAiChatResponse
                {
                    Content = content,
                    ToolCalls = toolCalls
                };
        }

        /// <inheritdoc />
        public string? ExtractErrorMessage(string responseBody)
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

                    if (error.TryGetProperty("message", out var messageElement))
                    {
                        return messageElement.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Swallow – we'll fall back to raw body
            }

            return responseBody.Length > 1024 ? responseBody.Substring(0, 1024) + "…" : responseBody;
        }

        private static string? AppendMessageText(string? current, JsonElement messageItem)
        {
            if (!messageItem.TryGetProperty("content", out var contentParts)
                || contentParts.ValueKind != JsonValueKind.Array)
            {
                return current;
            }

            foreach (var part in contentParts.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var partType)
                    && partType.GetString() == "output_text"
                    && part.TryGetProperty("text", out var textElement)
                    && textElement.ValueKind == JsonValueKind.String)
                {
                    current = current == null ? textElement.GetString() : current + textElement.GetString();
                }
            }

            return current;
        }

        private static IEnumerable<object> ToWireItems(ChatMessage message)
        {
            switch (message.Role)
            {
                case ParticipantType.Assistant when message.ToolCalls is { Count: > 0 }:
                    if (!string.IsNullOrEmpty(message.Text))
                    {
                        yield return new { role = "assistant", content = message.Text };
                    }

                    foreach (var tc in message.ToolCalls)
                    {
                        yield return new
                            {
                                type = "function_call",
                                call_id = tc.Id,
                                name = tc.FunctionName,
                                arguments = tc.ArgumentsJson ?? "{}"
                            };
                    }

                    break;

                case ParticipantType.Tool:
                    yield return new
                        {
                            type = "function_call_output",
                            call_id = message.ToolCallId ?? string.Empty,
                            output = message.Text ?? string.Empty
                        };

                    break;

                default:
                    yield return new { role = message.Role.ToString().ToLower(), content = message.Text };
                    break;
            }
        }

        private static object ToWireTool(ToolDescriptor descriptor)
        {
            // Responses API tool definitions are flat — no nested 'function' wrapper.
            using var doc = JsonDocument.Parse(descriptor.ParametersJsonSchema);

            return new
                {
                    type = "function",
                    name = descriptor.Name,
                    description = descriptor.Description,
                    parameters = doc.RootElement.Clone()
                };
        }
    }
}
