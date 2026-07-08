namespace PayRunIO.RqlAssistant.Service
{
    using System.Linq;
    using System.Text.Json;

    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Single source of truth for the in-process RQL assistant tool surface.
    /// Used directly by <see cref="RqlRagService"/> and re-exported as MCP tools by the Mcp project.
    /// </summary>
    public interface IRqlToolDispatcher
    {
        IReadOnlyList<ToolDescriptor> Descriptors { get; }

        string Dispatch(string toolName, JsonElement arguments);
    }

    public sealed class RqlToolDispatcher : IRqlToolDispatcher
    {
        private static readonly JsonSerializerOptions SerializerOptions =
            new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
                };

        private readonly IDocumentRepository repository;

        private readonly IQueryValidator validator;

        private readonly IRqlGrammarIndex grammarIndex;

        private readonly IRqlExampleIndex exampleIndex;

        private readonly IRqlSemanticLinter semanticLinter;

        private readonly IReadOnlyList<ToolDescriptor> descriptors;

        public RqlToolDispatcher(
            IDocumentRepository repository,
            IQueryValidator validator,
            IRqlGrammarIndex grammarIndex,
            IRqlExampleIndex exampleIndex,
            IRqlSemanticLinter semanticLinter)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.grammarIndex = grammarIndex ?? throw new ArgumentNullException(nameof(grammarIndex));
            this.exampleIndex = exampleIndex ?? throw new ArgumentNullException(nameof(exampleIndex));
            this.semanticLinter = semanticLinter ?? throw new ArgumentNullException(nameof(semanticLinter));
            this.descriptors = BuildDescriptors();
        }

        public IReadOnlyList<ToolDescriptor> Descriptors => this.descriptors;

        public string Dispatch(string toolName, JsonElement arguments)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return Error("Tool name is required.");
            }

            switch (toolName)
            {
                case "list_schemas":
                    return this.ListSchemas(arguments);
                case "get_schema":
                    return this.GetSchema(arguments);
                case "list_routes":
                    return this.ListRoutes(arguments);
                case "get_route":
                    return this.GetRoute(arguments);
                case "validate_query":
                    return this.ValidateQuery(arguments);
                case "list_rql_topics":
                    return this.ListRqlTopics();
                case "get_rql_syntax":
                    return this.GetRqlSyntax(arguments);
                case "list_examples":
                    return this.ListExamples(arguments);
                case "get_example":
                    return this.GetExample(arguments);
                default:
                    var available = string.Join(", ", this.descriptors.Select(d => "'" + d.Name + "'"));
                    return Error($"Unknown tool '{toolName}'. Available: {available}.");
            }
        }

        private string ListSchemas(JsonElement arguments)
        {
            var filter = TryGetString(arguments, "filter");

            var result = this.repository
                .ListSchemas(filter)
                .Select(ToSummary)
                .ToArray();

            return JsonSerializer.Serialize(result, SerializerOptions);
        }

        private string GetSchema(JsonElement arguments)
        {
            var typeName = TryGetString(arguments, "typeName");

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return Error("Argument 'typeName' is required.");
            }

            var schema = this.repository.GetSchema(typeName);

            return schema == null
                ? "null"
                : JsonSerializer.Serialize(ToFull(schema), SerializerOptions);
        }

        private string ListRoutes(JsonElement arguments)
        {
            var filter = TryGetString(arguments, "filter");
            var verb = TryGetString(arguments, "verb");
            var tag = TryGetString(arguments, "tag");

            var result = FilterRoutes(this.repository.GetRouteDefinitions(), filter, verb, tag)
                .Select(ToSummary)
                .ToArray();

            return JsonSerializer.Serialize(result, SerializerOptions);
        }

        /// <summary>
        /// Applies the list_routes filters: substring match on the route signature, exact verb
        /// match, exact tag match. All case-insensitive, ANDed, and each optional.
        /// </summary>
        public static IEnumerable<RouteDefinition> FilterRoutes(
            IEnumerable<RouteDefinition> routes,
            string? filter,
            string? verb,
            string? tag)
        {
            if (!string.IsNullOrWhiteSpace(filter))
            {
                routes = routes.Where(r => r.RouteSignature != null
                                           && r.RouteSignature.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(verb))
            {
                routes = routes.Where(r => string.Equals(r.Verb, verb, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                routes = routes.Where(r => r.Tags != null
                                           && r.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
            }

            return routes;
        }

        private string GetRoute(JsonElement arguments)
        {
            var className = TryGetString(arguments, "className");

            if (string.IsNullOrWhiteSpace(className))
            {
                return Error("Argument 'className' is required.");
            }

            var route = this.repository
                .GetRouteDefinitions()
                .FirstOrDefault(r => string.Equals(r.ClassName, className, StringComparison.OrdinalIgnoreCase));

            return route == null
                ? "null"
                : JsonSerializer.Serialize(ToFull(route), SerializerOptions);
        }

        private string ValidateQuery(JsonElement arguments)
        {
            var xml = TryGetString(arguments, "xml");

            if (string.IsNullOrWhiteSpace(xml))
            {
                return Error("Argument 'xml' is required.");
            }

            var result = this.validator.Validate(xml);

            // Semantic lint warnings ride along with the XSD diagnostics; they never affect IsValid.
            var lintDiagnostics = this.semanticLinter.Lint(xml);

            var dto = new ValidationResultDto
                {
                    IsValid = result.IsValid,
                    Diagnostics = result.Diagnostics.Concat(lintDiagnostics).Select(ToDto).ToArray()
                };

            return JsonSerializer.Serialize(dto, SerializerOptions);
        }

        private string ListRqlTopics()
        {
            var topics = this.grammarIndex.Topics
                .Select(t => new { slug = t.Slug, title = t.Title })
                .ToArray();

            return JsonSerializer.Serialize(topics, SerializerOptions);
        }

        private string GetRqlSyntax(JsonElement arguments)
        {
            var topic = TryGetString(arguments, "topic");

            if (string.IsNullOrWhiteSpace(topic))
            {
                return Error("Argument 'topic' is required.");
            }

            var body = this.grammarIndex.GetTopic(topic);

            if (body == null)
            {
                var available = string.Join(", ", this.grammarIndex.Topics.Select(t => "'" + t.Slug + "'"));
                return Error($"Unknown topic '{topic}'. Available: {available}.");
            }

            // Markdown body returned as a JSON string so it round-trips cleanly through tool-call replies.
            return JsonSerializer.Serialize(new { topic, content = body }, SerializerOptions);
        }

        private string ListExamples(JsonElement arguments)
        {
            var filter = TryGetString(arguments, "filter");

            var result = FilterExamples(this.exampleIndex.Examples, filter)
                .Select(e => new { slug = e.Slug, title = e.Title, request = e.Request, tags = e.Tags })
                .ToArray();

            return JsonSerializer.Serialize(result, SerializerOptions);
        }

        private string GetExample(JsonElement arguments)
        {
            var slug = TryGetString(arguments, "slug");

            if (string.IsNullOrWhiteSpace(slug))
            {
                return Error("Argument 'slug' is required.");
            }

            var example = this.exampleIndex.GetExample(slug);

            if (example == null)
            {
                var available = string.Join(", ", this.exampleIndex.Examples.Select(e => "'" + e.Slug + "'"));
                return Error($"Unknown example '{slug}'. Available: {available}.");
            }

            // Markdown body returned as a JSON string so it round-trips cleanly through tool-call replies.
            return JsonSerializer.Serialize(new { slug = example.Slug, content = example.Body }, SerializerOptions);
        }

        /// <summary>
        /// Case-insensitive keyword match over slug, title, request text and tags. Multiple
        /// whitespace-separated terms are ANDed so 'pension employee' narrows rather than widens.
        /// </summary>
        public static IEnumerable<RqlExample> FilterExamples(IEnumerable<RqlExample> examples, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return examples;
            }

            var terms = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return examples.Where(e => terms.All(term =>
                e.Slug.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.Request.Contains(term, StringComparison.OrdinalIgnoreCase)
                || e.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase))));
        }

        private static string? TryGetString(JsonElement arguments, string propertyName)
        {
            if (arguments.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!arguments.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Null => null,
                    JsonValueKind.Undefined => null,
                    _ => value.GetRawText()
                };
        }

        private static string Error(string message) =>
            JsonSerializer.Serialize(new { error = message }, SerializerOptions);

        public static SchemaSummaryDto ToSummary(ClassDefinition schema) =>
            new SchemaSummaryDto
                {
                    Name = schema.ClassName ?? string.Empty,
                    Description = schema.Description ?? string.Empty
                };

        public static SchemaDto ToFull(ClassDefinition schema) =>
            new SchemaDto
                {
                    Name = schema.ClassName ?? string.Empty,
                    Description = schema.Description ?? string.Empty,
                    Properties = (schema.Properties ?? new List<PropertyDefinition>())
                        .Select(p => new PropertyDto
                            {
                                Name = p.Name ?? string.Empty,
                                Type = p.Type ?? string.Empty,
                                Description = p.Description ?? string.Empty
                            })
                        .ToArray()
                };

        public static RouteSummaryDto ToSummary(RouteDefinition route) =>
            new RouteSummaryDto
                {
                    ClassName = route.ClassName ?? string.Empty,
                    Verb = route.Verb ?? string.Empty,
                    RouteSignature = route.RouteSignature ?? string.Empty,
                    Summary = route.Summary ?? string.Empty
                };

        public static RouteDto ToFull(RouteDefinition route) =>
            new RouteDto
                {
                    ClassName = route.ClassName ?? string.Empty,
                    Verb = route.Verb ?? string.Empty,
                    RouteSignature = route.RouteSignature ?? string.Empty,
                    OperationId = route.OperationId ?? string.Empty,
                    Summary = route.Summary ?? string.Empty,
                    Description = route.Description ?? string.Empty,
                    ResponseType = route.ResponseType ?? string.Empty,
                    Tags = (route.Tags ?? new List<string>()).ToArray()
                };

        public static ValidationDiagnosticDto ToDto(ValidationDiagnostic diagnostic) =>
            new ValidationDiagnosticDto
                {
                    Severity = diagnostic.Severity.ToString(),
                    Line = diagnostic.Line,
                    Column = diagnostic.Column,
                    Code = diagnostic.Code,
                    Message = diagnostic.Message
                };

        private static IReadOnlyList<ToolDescriptor> BuildDescriptors() =>
            new[]
                {
                    new ToolDescriptor(
                        "list_schemas",
                        RqlToolDescriptions.ListSchemas,
                        ParametersSchema(("filter", RqlToolDescriptions.ListSchemasFilterParam, false))),
                    new ToolDescriptor(
                        "get_schema",
                        RqlToolDescriptions.GetSchema,
                        ParametersSchema(("typeName", RqlToolDescriptions.GetSchemaTypeNameParam, true))),
                    new ToolDescriptor(
                        "list_routes",
                        RqlToolDescriptions.ListRoutes,
                        ParametersSchema(
                            ("filter", RqlToolDescriptions.ListRoutesFilterParam, false),
                            ("verb", RqlToolDescriptions.ListRoutesVerbParam, false),
                            ("tag", RqlToolDescriptions.ListRoutesTagParam, false))),
                    new ToolDescriptor(
                        "get_route",
                        RqlToolDescriptions.GetRoute,
                        ParametersSchema(("className", RqlToolDescriptions.GetRouteClassNameParam, true))),
                    new ToolDescriptor(
                        "validate_query",
                        RqlToolDescriptions.ValidateQuery,
                        ParametersSchema(("xml", RqlToolDescriptions.ValidateQueryXmlParam, true))),
                    new ToolDescriptor(
                        "list_rql_topics",
                        RqlToolDescriptions.ListRqlTopics,
                        ParametersSchema()),
                    new ToolDescriptor(
                        "get_rql_syntax",
                        RqlToolDescriptions.GetRqlSyntax,
                        ParametersSchema(("topic", RqlToolDescriptions.GetRqlSyntaxTopicParam, true))),
                    new ToolDescriptor(
                        "list_examples",
                        RqlToolDescriptions.ListExamples,
                        ParametersSchema(("filter", RqlToolDescriptions.ListExamplesFilterParam, false))),
                    new ToolDescriptor(
                        "get_example",
                        RqlToolDescriptions.GetExample,
                        ParametersSchema(("slug", RqlToolDescriptions.GetExampleSlugParam, true)))
                };

        /// <summary>
        /// Builds an OpenAI-style JSON Schema parameters object from (name, description, required)
        /// tuples. All tool parameters are strings, so the schema shape is uniform.
        /// </summary>
        private static string ParametersSchema(params (string Name, string Description, bool Required)[] parameters)
        {
            var properties = new Dictionary<string, object>();

            foreach (var (name, description, _) in parameters)
            {
                properties[name] = new { type = "string", description };
            }

            var schema = new
                {
                    type = "object",
                    properties,
                    required = parameters.Where(p => p.Required).Select(p => p.Name).ToArray()
                };

            return JsonSerializer.Serialize(schema, SerializerOptions);
        }
    }
}
