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
        Task<string> AskQuestion(
            string userQuestion,
            IEnumerable<ChatMessage>? chatHistory = null,
            ResponseType format = ResponseType.Conversation);
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
            ResponseType format = ResponseType.Conversation)
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                throw new ArgumentException("User question cannot be empty.", nameof(userQuestion));
            }

            this.EnsureInitialised();

            var conversation = this.BuildInitialConversation(userQuestion, chatHistory, format);

            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                var requestJson = this.requestBuilderService.CreateAiRequestJson(
                    conversation.ToArray(),
                    this.toolDispatcher.Descriptors);

                var response = await this.remoteAiService.GetChatResponseAsync(requestJson).ConfigureAwait(false);

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
