namespace PayRunIO.RqlAssistant.Service.Wire
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Anthropic Messages API wire format: <c>x-api-key</c>/<c>anthropic-version</c> auth, a top-level
    /// <c>system</c> field (not a system-role message), required <c>max_tokens</c>,
    /// <c>tools[{name,description,input_schema}]</c>, and <c>content[]</c> block responses
    /// (<c>type:"text"</c> / <c>type:"tool_use"</c>).
    /// </summary>
    public sealed class AnthropicWireFormat : IChatWireFormat
    {
        private const string PathSuffix = "/v1/messages";

        private const string AnthropicVersion = "2023-06-01";

        private const int DefaultMaxTokens = 4096;

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
            // Anthropic takes the system prompt as a single top-level string, not a message in the array.
            var systemText = string.Join(
                "\n\n",
                chatPrompts.Where(p => p.Role == ParticipantType.System).Select(p => p.Text));

            var messages = chatPrompts
                .Where(p => p.Role != ParticipantType.System)
                .Select(ToWireMessage)
                .ToArray();

            var payload = new Dictionary<string, object?>
                {
                    ["model"] = model,
                    ["messages"] = messages,
                    ["temperature"] = temperature,
                    ["max_tokens"] = DefaultMaxTokens
                };

            if (!string.IsNullOrEmpty(systemText))
            {
                payload["system"] = systemText;
            }

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = tools.Select(ToWireTool).ToArray();
            }

            return JsonSerializer.Serialize(payload, this.jsonSerializerOptions);
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
            httpClient.DefaultRequestHeaders.Remove("x-api-key");
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

            httpClient.DefaultRequestHeaders.Remove("anthropic-version");
            httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        }

        /// <inheritdoc />
        public OpenAiChatResponse ParseResponse(string responseBody, HttpStatusCode statusCode)
        {
            using var doc = JsonDocument.Parse(responseBody);

            if (!doc.RootElement.TryGetProperty("content", out var contentBlocks)
                || contentBlocks.ValueKind != JsonValueKind.Array)
            {
                throw new OpenAiException("Response JSON missing 'content[]'.", statusCode);
            }

            string? content = null;
            var toolCalls = new List<OpenAiToolCall>();

            foreach (var block in contentBlocks.EnumerateArray())
            {
                if (!block.TryGetProperty("type", out var typeElement))
                {
                    continue;
                }

                var blockType = typeElement.GetString();

                if (blockType == "text"
                    && block.TryGetProperty("text", out var textElement)
                    && textElement.ValueKind == JsonValueKind.String)
                {
                    content = content == null ? textElement.GetString() : content + textElement.GetString();
                }
                else if (blockType == "tool_use")
                {
                    var id = block.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                                 ? idElement.GetString() ?? string.Empty
                                 : string.Empty;

                    var name = block.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                                   ? nameElement.GetString() ?? string.Empty
                                   : string.Empty;

                    var argumentsJson = block.TryGetProperty("input", out var inputElement)
                                             ? inputElement.GetRawText()
                                             : "{}";

                    toolCalls.Add(new OpenAiToolCall(id, name, argumentsJson));
                }
            }

            if (content == null && toolCalls.Count == 0)
            {
                throw new OpenAiException("Response JSON contained no text or tool_use content blocks.", statusCode);
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

                if (doc.RootElement.TryGetProperty("error", out var error)
                    && error.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString();
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
                    {
                        var contentBlocks = new List<object>();

                        if (!string.IsNullOrEmpty(message.Text))
                        {
                            contentBlocks.Add(new { type = "text", text = message.Text });
                        }

                        contentBlocks.AddRange(message.ToolCalls.Select(tc => (object)new
                            {
                                type = "tool_use",
                                id = tc.Id,
                                name = tc.FunctionName,
                                input = ParseArguments(tc.ArgumentsJson)
                            }));

                        return new { role = "assistant", content = contentBlocks.ToArray() };
                    }

                case ParticipantType.Tool:
                    // Anthropic surfaces tool results as a user-role message containing a tool_result block,
                    // not a dedicated "tool" role.
                    return new
                        {
                            role = "user",
                            content = new object[]
                                {
                                    new
                                        {
                                            type = "tool_result",
                                            tool_use_id = message.ToolCallId ?? string.Empty,
                                            content = message.Text ?? string.Empty
                                        }
                                }
                        };

                default:
                    return new { role = message.Role.ToString().ToLower(), content = message.Text };
            }
        }

        private static object ToWireTool(ToolDescriptor descriptor)
        {
            using var doc = JsonDocument.Parse(descriptor.ParametersJsonSchema);

            return new
                {
                    name = descriptor.Name,
                    description = descriptor.Description,
                    input_schema = doc.RootElement.Clone()
                };
        }

        private static JsonElement ParseArguments(string? argumentsJson)
        {
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(argumentsJson) ? "{}" : argumentsJson);
            return doc.RootElement.Clone();
        }
    }
}
