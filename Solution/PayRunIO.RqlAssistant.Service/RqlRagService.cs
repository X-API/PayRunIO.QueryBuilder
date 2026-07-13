namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text.Json;

    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Contract for the natural-language → RQL pipeline. The model is driven via OpenAI tool-calling
    /// against <see cref="IRqlToolDispatcher"/> rather than prompt-stuffing the full grammar.
    /// </summary>
    public interface IRqlRagService
    {
        /// <param name="userQuestion">The user prompt to answer.</param>
        /// <param name="chatHistory">Prior turns to replay as conversation context.</param>
        /// <param name="format">The reply style directive.</param>
        /// <param name="onActivity">Optional progress sink: receives a short human-readable line for
        /// each model round trip and tool invocation. May be invoked from a thread-pool thread.</param>
        /// <param name="cancellationToken">Cancels the in-flight model request and the tool loop.</param>
        Task<string> AskQuestion(
            string userQuestion,
            IEnumerable<ChatMessage>? chatHistory = null,
            ResponseType format = ResponseType.Conversation,
            Action<string>? onActivity = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class RqlRagService : IRqlRagService
    {
        /// <summary>
        /// Maximum tool-call round trips per question. A complete walk (examples, schema ×2, route,
        /// grammar ×2, validate, fix, validate, finalise) takes ~10; the buffer allows for extra
        /// validate/fix cycles now that warnings must be resolved too before finalising.
        /// </summary>
        private const int MaxIterations = 15;

        private readonly IRequestBuilderService requestBuilderService;

        private readonly IRemoteAiService remoteAiService;

        private readonly IRqlToolDispatcher toolDispatcher;

        private readonly object syncLock = new object();

        private bool isInitialised;

        private string answerQuestionSystemPrompt = string.Empty;

        private string grammarPrimer = string.Empty;

        private string tabularRqlResource = string.Empty;

        public RqlRagService(
            IRequestBuilderService requestBuilderService,
            IRemoteAiService remoteAiService,
            IRqlToolDispatcher toolDispatcher)
        {
            this.requestBuilderService = requestBuilderService ?? throw new ArgumentNullException(nameof(requestBuilderService));
            this.remoteAiService = remoteAiService ?? throw new ArgumentNullException(nameof(remoteAiService));
            this.toolDispatcher = toolDispatcher ?? throw new ArgumentNullException(nameof(toolDispatcher));
        }

        public async Task<string> AskQuestion(
            string userQuestion,
            IEnumerable<ChatMessage>? chatHistory = null,
            ResponseType format = ResponseType.Conversation,
            Action<string>? onActivity = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                throw new ArgumentException("User question cannot be empty.", nameof(userQuestion));
            }

            this.EnsureInitialised();

            var conversation = this.BuildInitialConversation(userQuestion, chatHistory, format);

            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestJson = this.requestBuilderService.CreateAiRequestJson(
                    conversation.ToArray(),
                    this.toolDispatcher.Descriptors);

                onActivity?.Invoke(iteration == 0 ? "Consulting the AI model" : "Waiting for the AI model's next step");

                var response = await this.remoteAiService.GetChatResponseAsync(requestJson, cancellationToken).ConfigureAwait(false);

                if (!response.HasToolCalls)
                {
                    return response.Content ?? string.Empty;
                }

                // Assistant turn with the tool_calls (no final content yet) — must be preserved verbatim
                // so the matching tool reply messages line up with its tool_call ids.
                conversation.Add(new ChatMessage
                    {
                        Role = ParticipantType.Assistant,
                        Text = response.Content ?? string.Empty,
                        ToolCalls = response.ToolCalls
                    });

                foreach (var call in response.ToolCalls)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    onActivity?.Invoke(DescribeToolCall(call));

                    conversation.Add(new ChatMessage
                        {
                            Role = ParticipantType.Tool,
                            ToolCallId = call.Id,
                            Text = this.DispatchToolCall(call)
                        });
                }
            }

            throw new OpenAiException(
                $"RQL assistant exceeded the maximum of {MaxIterations} tool-call iterations without producing a final reply.");
        }

        /// <summary>
        /// Renders a tool call as a short human-readable progress line, e.g.
        /// "Reading the 'Employee' schema" — shown in the UI while the agent loop runs.
        /// </summary>
        private static string DescribeToolCall(OpenAiToolCall call)
        {
            var subject = ExtractToolCallSubject(call.ArgumentsJson);

            return call.FunctionName switch
                {
                    "list_schemas" => "Browsing the entity schemas",
                    "get_schema" => subject == null ? "Reading an entity schema" : $"Reading the '{subject}' schema",
                    "list_routes" => "Browsing the API routes",
                    "get_route" => subject == null ? "Reading an API route" : $"Reading the '{subject}' route",
                    "validate_query" => "Validating the generated query",
                    "list_rql_topics" => "Browsing the RQL syntax topics",
                    "get_rql_syntax" => subject == null ? "Reading RQL syntax guidance" : $"Reading RQL syntax: '{subject}'",
                    "list_examples" => "Browsing the example queries",
                    "get_example" => subject == null ? "Reading an example query" : $"Reading example query '{subject}'",
                    _ => $"Running tool '{call.FunctionName}'",
                };
        }

        /// <summary>
        /// Pulls the most identifying string argument out of a tool call's JSON arguments, trying
        /// the known key-argument names used across the tool surface.
        /// </summary>
        private static string? ExtractToolCallSubject(string? argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(argumentsJson);

                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                foreach (var key in new[] { "typeName", "className", "topic", "slug", "filter" })
                {
                    if (doc.RootElement.TryGetProperty(key, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed arguments are reported to the model by DispatchToolCall; the progress
                // line just falls back to the generic description.
            }

            return null;
        }

        private string DispatchToolCall(OpenAiToolCall call)
        {
            JsonElement arguments;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
                arguments = doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(new
                    {
                        error = $"Tool '{call.FunctionName}' arguments are not valid JSON: {ex.Message}"
                    });
            }

            return this.toolDispatcher.Dispatch(call.FunctionName, arguments);
        }

        private Collection<ChatMessage> BuildInitialConversation(
            string userQuestion,
            IEnumerable<ChatMessage>? chatHistory,
            ResponseType format)
        {
            var conversation = new Collection<ChatMessage>();

            conversation.Add(new ChatMessage { Role = ParticipantType.System, Text = this.answerQuestionSystemPrompt });
            conversation.Add(new ChatMessage { Role = ParticipantType.System, Text = this.grammarPrimer });
            conversation.Add(new ChatMessage
                {
                    Role = ParticipantType.System,
                    Text = "Use the available tools to ground your reply: 'list_schemas'/'get_schema' for entity shapes, "
                           + "'list_routes'/'get_route' for API route URLs to use in Group Selector attributes, "
                           + "'list_rql_topics'/'get_rql_syntax' for grammar details, and 'validate_query' to check XML before finalising. "
                           + "Resolve every validate_query diagnostic, warnings included, before replying — warnings almost always "
                           + "indicate a real mistake (unknown route, unknown property, unassigned variable, misplaced Order/Filter). "
                           + "Do not invent property names, route URLs, or RQL syntax — look them up."
                });

            conversation.Add(new ChatMessage { Role = ParticipantType.System, Text = FormatDirective(format) });

            if (format == ResponseType.TabularQuery)
            {
                conversation.Add(new ChatMessage { Role = ParticipantType.System, Text = this.tabularRqlResource });
            }

            if (chatHistory != null)
            {
                foreach (var message in chatHistory)
                {
                    conversation.Add(message);
                }
            }

            conversation.Add(new ChatMessage { Role = ParticipantType.User, Text = userQuestion });

            return conversation;
        }

        private static string FormatDirective(ResponseType format) =>
            format switch
                {
                    ResponseType.XmlOnly =>
                        "**Respond ONLY with the RQL statement enclosed in triple back-ticks formatted as 'XML'. XML must not contain non-ASCII characters. Do not add explanations. Do not include XML comments.**",
                    ResponseType.Conversation =>
                        "**Respond conversationally to the user prompt using markdown syntax. When responding with RQL statements, ensure they are in 'XML' format and wrapped in triple back-ticks. XML must not contain non-ASCII characters. Do not include XML comments.**",
                    ResponseType.TabularQuery =>
                        "**Respond conversationally to the user prompt using markdown syntax**. When responding with RQL statements, strictly enforce the use of the **Tabular Output Pattern** and use RQL in 'XML' format wrapped in triple back-ticks. Do not include XML comments.",
                    _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
                };

        private void EnsureInitialised()
        {
            if (this.isInitialised)
            {
                return;
            }

            lock (this.syncLock)
            {
                if (this.isInitialised)
                {
                    return;
                }

                var primerTask = ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.RqlGrammarPrimer);
                var systemPromptTask = ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.AnswerQuestionSystemPrompt);
                var tabularTask = ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.TabularRql);

                Task.WaitAll(primerTask, systemPromptTask, tabularTask);

                this.grammarPrimer = primerTask.Result;
                this.answerQuestionSystemPrompt = systemPromptTask.Result;
                this.tabularRqlResource = tabularTask.Result;

                this.isInitialised = true;
            }
        }
    }
}
