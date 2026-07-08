namespace PayRunIO.RqlAssistant.Service.Wire
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;

    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;

    using PayRunIO.RqlAssistant.Service;

    /// <summary>
    /// OpenAI Chat Completions wire format: <c>Authorization: Bearer</c> auth, system-role messages inline
    /// in <c>messages[]</c>, <c>tools[{type:"function",function:{...}}]</c>, <c>choices[0].message</c> responses.
    /// </summary>
    public sealed class OpenAiWireFormat : IChatWireFormat
    {
        private const string PathSuffix = "/v1/chat/completions";

        private readonly JsonSerializerOptions jsonSerializerOptions =
            new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

        /// <inheritdoc />
        public string BuildRequestJson(
            ChatMessage[] chatPrompts,
            IReadOnlyList<ToolDescriptor>? tools,
            string model,
            double temperature)
        {
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
                        model,
                        messages = messages.ToArray(),
                        temperature,
                        tools = tools.Select(ToWireTool).ToArray(),
                        tool_choice = "auto"
                    };
            }
            else
            {
                requestPayload = new
                    {
                        model,
                        messages = messages.ToArray(),
                        temperature
                    };
            }

            return JsonSerializer.Serialize(requestPayload, this.jsonSerializerOptions);
        }

        /// <inheritdoc />
        public string BuildRequestUrl(string host)
        {
            var trimmedHost = host.TrimEnd('/');

            return trimmedHost.EndsWith(PathSuffix, StringComparison.OrdinalIgnoreCase)
                       ? trimmedHost
                       : trimmedHost + PathSuffix;
        }

        /// <inheritdoc />
        public void ApplyAuthHeaders(HttpClient httpClient, string apiKey)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        /// <inheritdoc />
        public OpenAiChatResponse ParseResponse(string responseBody, HttpStatusCode statusCode)
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

                    return error.GetProperty("message").GetString();
                }
            }
            catch (JsonException)
            {
                // Swallow – we'll fall back to raw body
            }

            return responseBody.Length > 1024 ? responseBody.Substring(0, 1024) + "…" : responseBody;
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
