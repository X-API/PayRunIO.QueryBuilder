namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;

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
        /// <param name="includeSchemasAndRoutes"></param>
        /// <param name="chatHistory">The chat History.</param>
        /// <param name="format">Determines the RQL response formatting. Either JSON, XML or Conversation.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        Task<string> AskQuestion(
            string userQuestion,
            bool includeSchemasAndRoutes = true,
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
        /// The find schema and route names resource.
        /// </summary>
        private string findSchemaAndRouteNamesResource;

        /// <summary>
        /// The cheat sheet resource.
        /// </summary>
        private string cheatSheetResource_XXX;

        /// <summary>
        /// The answer question system prompt.
        /// </summary>
        private string answerQuestionSystemPrompt;

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
        /// <param name="includeSchemasAndRoutes"></param>
        /// <param name="chatHistory">The chat History.</param>
        /// <param name="format">Determines the RQL response formatting. Either JSON, XML or Conversation.</param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public async Task<string> AskQuestion(
            string userQuestion,
            bool includeSchemasAndRoutes = true,
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

            // Discover schemas and routes in user question
            var results = new SchemaAndRoutes();

            if (includeSchemasAndRoutes)
            {
                var getSchemaPrompt = this.BuildSchemaAndRouteExtractionRequest(userQuestion);

                var schemaAndRouteResponse = await this.remoteAiService.GetResponseAsync(getSchemaPrompt);

                /*
                 * Example Response:
                 *
                 * {
                 *   "routes": ["RouteNameA", "RouteNameB"],
                 *   "schemas": ["SchemaNameA", "SchemaNameB"]
                 * }
                 *
                 */

                var match = Regex.Match(
                    schemaAndRouteResponse,
                    "\\{\\s*\"routes\"\\s*:\\s*\\[.*?\\],\\s*\"schemas\"\\s*:\\s*\\[.*?\\]\\s*\\}");

                if (match.Success)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    results = JsonSerializer.Deserialize<SchemaAndRoutes>(match.Value, options)
                              ?? throw new InvalidOperationException("Failed to deserialize the JSON content.");
                }
            }

            // Build augmented prompt
            var augmentedPrompt = this.BuildQueryGenerationRequest(userQuestion, results.schemas, results.routes, chatHistory, format);

            // Get final answer
            var answer = await this.remoteAiService.GetResponseAsync(augmentedPrompt);

            return answer;
        }

        public class SchemaAndRoutes
        {
            public string[] routes { get; set; } = new string[0];

            public string[] schemas { get; set; } = new string[0];
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
                        Task.Run(() => ResourceHelper.LoadResourceAsStringAsync(ResourceHelper.FindSchemaAndRouteNames))
                            .ContinueWith(t => this.findSchemaAndRouteNamesResource = t.Result),
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
        private string BuildSchemaAndRouteExtractionRequest(string userQuestion)
        {
            var chatMessages =
                new Collection<ChatMessage>
                        {
                            new ChatMessage { Role = ParticipantType.System, Text = this.findSchemaAndRouteNamesResource },
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
                        "RQL Full Documentation:\r\n" + this.documentRepository.GetDocumentation(format == ResponseType.JsonOnly ? "JSON" : "XML")
                    };

            switch (format)
            {
                case ResponseType.JsonOnly:
                    systemPrompts.Add("**Respond ONLY with the RQL statement enclosed in triple back‑ticks formatted as 'JSON'. Do not add explanations.**");
                    break;
                case ResponseType.XmlOnly:
                    systemPrompts.Add("**Respond ONLY with the RQL statement enclosed in triple back‑ticks formatted as 'XML'. XML Must not contain non-ASCII characters. Do not add explanations.** Do not include XML comments.");
                    break;
                case ResponseType.Conversation:
                    systemPrompts.Add("**Respond conversationally to the user prompt using markdown syntax. When responding with RQL statements, ensure they are in 'XML' format and wrapped in triple backticks. XML Must not contain non-ASCII characters.** Do not include XML comments.");
                    break;
                case ResponseType.TabularQuery:
                    systemPrompts.Add(this.tabularRqlResource);
                    systemPrompts.Add("**Respond conversationally to the user prompt using markdown syntax**. When responding with RQL statements, strictly enforce the use of the **Tabular Output Pattern** and use RQL in 'XML' format and **wrapped in triple backticks**. Do not include XML comments.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            // Fetch documentation snippets relevant to the identified schemaNames.
            if (schemaNames.Any())
            {
                // systemPrompts.Add(this.querySchema);
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
