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

        private readonly IReadOnlyList<ToolDescriptor> descriptors;

        public RqlToolDispatcher(IDocumentRepository repository, IQueryValidator validator, IRqlGrammarIndex grammarIndex)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.grammarIndex = grammarIndex ?? throw new ArgumentNullException(nameof(grammarIndex));
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

            IEnumerable<RouteDefinition> routes = this.repository.GetRouteDefinitions();

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

            var result = routes.Select(ToSummary).ToArray();

            return JsonSerializer.Serialize(result, SerializerOptions);
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

            var dto = new ValidationResultDto
                {
                    IsValid = result.IsValid,
                    Diagnostics = result.Diagnostics.Select(ToDto).ToArray()
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
                        "List all PayRunIO entity schemas. Returns name and description only — call get_schema for full property details. Optionally filter by a case-insensitive substring match on the schema name.",
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""filter"": {
                                    ""type"": ""string"",
                                    ""description"": ""Optional case-insensitive substring filter applied to schema names. Omit to list all schemas.""
                                }
                            },
                            ""required"": []
                        }"),
                    new ToolDescriptor(
                        "get_schema",
                        "Get the full definition of a single PayRunIO entity schema, including all of its properties. Use this to ground RQL queries against the real shape of entities like Employee, EmployeeSummary, PayRun, etc. Match is exact and case-insensitive; returns null if the name is unknown.",
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""typeName"": {
                                    ""type"": ""string"",
                                    ""description"": ""The exact schema type name, e.g. 'Employee', 'EmployeeSummary', 'PayRun'. Case-insensitive.""
                                }
                            },
                            ""required"": [""typeName""]
                        }"),
                    new ToolDescriptor(
                        "list_routes",
                        "List PayRunIO API routes. Returns class name, verb, URL template and a short summary — call get_route for the full description and response type. Filters are optional and ANDed together.",
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""filter"": {
                                    ""type"": ""string"",
                                    ""description"": ""Optional case-insensitive substring filter applied to the route URL template (RouteSignature). E.g. 'Employee' matches '/Employer/{employerId}/Employee/{employeeId}'.""
                                },
                                ""verb"": {
                                    ""type"": ""string"",
                                    ""description"": ""Optional HTTP verb filter, case-insensitive exact match. E.g. 'GET', 'POST', 'PUT', 'DELETE', 'PATCH'.""
                                },
                                ""tag"": {
                                    ""type"": ""string"",
                                    ""description"": ""Optional tag filter, case-insensitive exact match against any tag on the route. E.g. 'Employee', 'PayRun', 'Reports'.""
                                }
                            },
                            ""required"": []
                        }"),
                    new ToolDescriptor(
                        "get_route",
                        "Get the full definition of a single PayRunIO API route by its class name (the unique key returned by list_routes). Match is exact and case-insensitive; returns null if the class name is unknown.",
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""className"": {
                                    ""type"": ""string"",
                                    ""description"": ""The exact route class name, e.g. 'GetEmployeeRoute', 'GetAEAssessmentFromEmployeeRoute'. Case-insensitive.""
                                }
                            },
                            ""required"": [""className""]
                        }"),
                    new ToolDescriptor(
                        "validate_query",
                        "Validate a candidate RQL <Query> XML document against the PayRunIO QuerySchema.xsd. Returns structured diagnostics (line, column, code, message) so a caller can fix the query and retry. IsValid is true only when no Error-level diagnostics are produced; Warnings do not invalidate the query.",
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""xml"": {
                                    ""type"": ""string"",
                                    ""description"": ""The full RQL query XML to validate, starting at the <Query> root element.""
                                }
                            },
                            ""required"": [""xml""]
                        }"),
                    new ToolDescriptor(
                        "list_rql_topics",
                        "List every available RQL grammar topic that can be fetched with get_rql_syntax. Cheap to call — returns just slug + title for each topic. Use this to discover what's available before guessing topic names.",
                        @"{
                            ""type"": ""object"",
                            ""properties"": {},
                            ""required"": []
                        }"),
                    new ToolDescriptor(
                        "get_rql_syntax",
                        "Fetch a section of the RQL grammar documentation by topic slug. Returns the markdown for that section, including XML examples. Call list_rql_topics first if unsure which slug to use. Topics cover constructs like filters, ordering, conditions, outputs, variables, loop expressions, advanced features, etc.",
                        @"{
                            ""type"": ""object"",
                            ""properties"": {
                                ""topic"": {
                                    ""type"": ""string"",
                                    ""description"": ""The topic slug, e.g. 'filters', 'ordering', 'conditions-and-conditional-group-logic', 'outputs', 'variables', 'loop-expressions'. Case-insensitive.""
                                }
                            },
                            ""required"": [""topic""]
                        }")
                };
    }
}
