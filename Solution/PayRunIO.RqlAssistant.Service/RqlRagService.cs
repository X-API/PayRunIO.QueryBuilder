namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Contract for the two‑phase Retrieval‑Augmented Generation (RAG) pipeline that turns a natural‑language
    /// question into an RQL query suggestion.
    /// </summary>
    public interface IRqlRagService
    {
        /// <summary>
        /// The ask question method.
        /// </summary>
        /// <param name="userQuestion">The user question.</param>
        /// <param name="includeSchemas">Determines if the AI should attempt to find related schemas.</param>
        /// <param name="includeApiRoutes">Determines is the AI should attempt to find related API routes.</param>
        /// <param name="chatHistory">The chat History.</param>
        /// <param name="format">Determines the RQL response formatting. Either JSON, XML or Conversation.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        Task<string> AskQuestion(
            string userQuestion,
            bool includeSchemas = true,
            bool includeApiRoutes = true,
            IEnumerable<ChatMessage>? chatHistory = null,
            ResponseType format = ResponseType.Conversation);
    }

    /// <summary>
    ///     Skeleton implementation of <see cref="IRqlRagService"/>. All heavy lifting is delegated to the
    ///     <see cref="IRequestBuilderService"/> for JSON shaping; this class focuses on prompt engineering and
    ///     document retrieval orchestration.
    /// </summary>
    internal sealed class RqlRagService : IRqlRagService
    {
        /// <summary>
        /// The AI request service.
        /// </summary>
        private readonly IRequestBuilderService requestBuilderService;

        /// <summary>
        /// The document repository.
        /// </summary>
        private readonly DocumentRepository documentRepository;

        /// <summary>
        /// The remote AI service.
        /// </summary>
        private readonly IRemoteAiService remoteAiService;

        /// <summary>
        /// The synchronisation lock instance.
        /// </summary>
        private readonly object syncLock = new object();

        /// <summary>
        /// The is initialised value. Determines if the service has been initialised.
        /// </summary>
        private bool isInitialised = false;

        /// <summary>
        /// The find schema names resource.
        /// </summary>
        private string findSchemaNamesResource;

        /// <summary>
        /// The cheat sheet resource.
        /// </summary>
        private string cheatSheetResource;

        /// <summary>
        /// The answer question system prompt.
        /// </summary>
        private string answerQuestionSystemPrompt;

        /// <summary>
        /// The find route names resource.
        /// </summary>
        private string findRouteNamesResource;

        /// <summary>
        /// The tabular rql resource.
        /// </summary>
        private string tabularRqlResource;

        /// <summary>
        /// The query schema.
        /// </summary>
        private string querySchema;

        /// <summary>
        /// Initializes a new instance of the <see cref="RqlRagService"/> class.
        /// </summary>
        /// <param name="requestBuilderService">The request Builder Service.</param>
        /// <param name="documentRepository">The document Repository.</param>
        /// <param name="remoteAiService">The remote AI service.</param>
        public RqlRagService(IRequestBuilderService requestBuilderService, DocumentRepository documentRepository, IRemoteAiService remoteAiService)
        {
            this.requestBuilderService = requestBuilderService ?? throw new ArgumentNullException(nameof(requestBuilderService));
            this.documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
            this.remoteAiService = remoteAiService ?? throw new ArgumentNullException(nameof(remoteAiService));
        }

        /// <summary>
        /// The ask question method.
        /// </summary>
        /// <param name="userQuestion">The user question.</param>
        /// <param name="includeSchemas">Determines if the AI should attempt to find related schemas.</param>
        /// <param name="includeApiRoutes">Determines is the AI should attempt to find related API routes.</param>
        /// <param name="chatHistory">The chat History.</param>
        /// <param name="format">Determines the RQL response formatting. Either JSON, XML or Conversation.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public async Task<string> AskQuestion(
            string userQuestion, 
            bool includeSchemas = true, 
            bool includeApiRoutes = true, 
            IEnumerable<ChatMessage>? chatHistory = null, 
            ResponseType format = ResponseType.Conversation)
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                throw new ArgumentException("User question cannot be empty.", nameof(userQuestion));
            }

            if (!this.isInitialised)
            {
                lock (this.syncLock)
                {
                    if (!this.isInitialised)
                    {
                        this.LoadStaticResources();
                        this.isInitialised = true;
                    }
                }
            }

            // Discover schemas in user question
            var schemaNameArray = Array.Empty<string>();
            if (includeSchemas)
            {
                var getSchemaPrompt = this.BuildSchemaExtractionRequest(userQuestion);

                var schemaResponse = await this.remoteAiService.GetResponseAsync(getSchemaPrompt);

                schemaNameArray = schemaResponse?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray() ?? Array.Empty<string>();
            }

            // Discover API routes associated with user question
            var routeNameArray = Array.Empty<string>();
            if (includeApiRoutes)
            {
                var getRouteNamesPrompt = this.BuildRouteMatchRequest(userQuestion);

                var routeNameResponse = await this.remoteAiService.GetResponseAsync(getRouteNamesPrompt);

                routeNameArray = routeNameResponse?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray() ?? Array.Empty<string>();
            }

            // Build augmented prompt
            var augmentedPrompt = this.BuildQueryGenerationRequest(userQuestion, schemaNameArray, routeNameArray, chatHistory, format);

            // Get final answer
            var answer = await this.remoteAiService.GetResponseAsync(augmentedPrompt);

            return answer;
        }

        /// <summary>
        /// The build route match request.
        /// </summary>
        /// <param name="userQuestion">
        /// The user question.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private string BuildRouteMatchRequest(string userQuestion)
        {
            var allRouteNames = string.Join("\r\n * ", this.documentRepository.GetRouteDefinitions().Select(def => def.ClassName));

            var findRoutesSystemPrompt = this.findRouteNamesResource + allRouteNames;

            var chatMessages = 
                new Collection<ChatMessage>
                    {
                        new ChatMessage { Role = ParticipantType.System, Text = findRoutesSystemPrompt },
                        new ChatMessage { Role = ParticipantType.User, Text = userQuestion }
                    }
                    .ToArray();

            return this.requestBuilderService.CreateAiRequestJson(chatMessages);
        }

        /// <summary>
        /// The load static resources.
        /// </summary>
        private void LoadStaticResources()
        {
            var tasks = 
                new Task[]
                    {
                        Task.Run(() => ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.TabularRql))
                            .ContinueWith(t => this.tabularRqlResource = t.Result),
                        Task.Run(() => ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.QuerySchema))
                            .ContinueWith(t => this.querySchema = t.Result),
                        Task.Run(() => ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.FindSchemaNames))
                            .ContinueWith(t => this.findSchemaNamesResource = t.Result),
                        Task.Run(() => ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.FindRouteNames))
                            .ContinueWith(t => this.findRouteNamesResource = t.Result),
                        Task.Run(() => ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.RqlCheatSheet))
                            .ContinueWith(t => this.cheatSheetResource = t.Result),
                        Task.Run(() => ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.AnswerQuestionSystemPrompt))
                            .ContinueWith(t => this.answerQuestionSystemPrompt = t.Result),
                    };

            Task.WaitAll(tasks);
        }

        /// <summary>
        /// Builds the first‑pass request that asks the LLM to identify relevant RQL schemas.
        /// </summary>
        /// <param name="userQuestion">The raw end‑user question.</param>
        /// <returns>JSON request string suitable for posting to the chat completions endpoint.</returns>
        private string BuildSchemaExtractionRequest(string userQuestion)
        {
            var chatMessages =
                new Collection<ChatMessage>
                        {
                            new ChatMessage { Role = ParticipantType.System, Text = this.findSchemaNamesResource },
                            new ChatMessage { Role = ParticipantType.System, Text = this.cheatSheetResource },
                            new ChatMessage { Role = ParticipantType.User, Text = userQuestion }
                        }
                    .ToArray();

            return this.requestBuilderService.CreateAiRequestJson(chatMessages);
        }

        /// <summary>
        /// The build query generation request method.
        /// </summary>
        /// <param name="userQuestion">The user question.</param>
        /// <param name="schemaNames">The schema Names.</param>
        /// <param name="routeNames">The API route names.</param>
        /// <param name="chatHistory">The chat history.</param>
        /// <param name="format">The content type format.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// User question cannot be null or empty.
        /// </exception>
        private string BuildQueryGenerationRequest(
            string userQuestion,
            string[] schemaNames,
            string[] routeNames,
            IEnumerable<ChatMessage>? chatHistory,
            ResponseType format)
        {
            var systemPrompts = 
                new List<string>
                    {
                        this.answerQuestionSystemPrompt,
                        "RQL Quick Reference Cheat Sheet:\r\n" + this.cheatSheetResource,
                        "RQL Full Documentation:\r\n" + this.documentRepository.GetDocumentation(format == ResponseType.JsonOnly ? "JSON" : "XML")
                    };

            switch (format)
            {
                case ResponseType.JsonOnly:
                    systemPrompts.Add("**Respond ONLY with the RQL statement enclosed in triple back‑ticks formatted as 'JSON'. Do not add explanations.**");
                    break;
                case ResponseType.XmlOnly:
                    systemPrompts.Add("**Respond ONLY with the RQL statement enclosed in triple back‑ticks formatted as 'XML'. Do not add explanations.**");
                    break;
                case ResponseType.Conversation:
                    systemPrompts.Add("**Respond conversationally to the user prompt using markdown syntax. When responding with RQL statements, ensure they are in 'XML' format and wrapped in triple backticks.**");
                    break;
                case ResponseType.TabularQuery:
                    systemPrompts.Add(this.tabularRqlResource);
                    systemPrompts.Add("**Respond conversationally to the user prompt using markdown syntax**. When responding with RQL statements, strictly enforce the use of the **Tabular Output Pattern** and use RQL in 'XML' format and **wrapped in triple backticks.**");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            // Fetch documentation snippets relevant to the identified schemaNames.
            if (schemaNames.Any())
            {
                //systemPrompts.Add(this.querySchema);
                systemPrompts.AddRange(this.documentRepository.FindSchemaSnippets(schemaNames).Select(d => "# Entity Schema:\r\n" + d));
            }

            // Fetch documentation snippets relevant to the identified route names.
            if (routeNames.Any())
            {
                var routeDefinitions = this.documentRepository.GetRouteDefinitions().Where(def => routeNames.Contains(def.ClassName));

                systemPrompts.AddRange(routeDefinitions.Select(def => def.ToString()));
            }

            var chatMessages = new Collection<ChatMessage>();
            foreach (var systemPrompt in systemPrompts)
            {
                chatMessages.Add(new ChatMessage { Role = ParticipantType.System, Text = systemPrompt });
            }

            if (chatHistory != null && chatHistory.Any())
            {
                foreach (var message in chatHistory)
                {
                    chatMessages.Add(message);
                }
            }

            chatMessages.Add(new ChatMessage { Role = ParticipantType.User, Text = userQuestion });

            return this.requestBuilderService.CreateAiRequestJson(chatMessages.ToArray());
        }
    }
}
