namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Linq;
    using System.Text.Json;

    using Moq;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Models;

    [TestFixture]
    public class RqlToolDispatcherTests
    {
        private Mock<IDocumentRepository> repository = null!;

        private Mock<IQueryValidator> validator = null!;

        private Mock<IRqlGrammarIndex> grammarIndex = null!;

        private RqlToolDispatcher dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            this.repository = new Mock<IDocumentRepository>(MockBehavior.Strict);
            this.validator = new Mock<IQueryValidator>(MockBehavior.Strict);
            this.grammarIndex = new Mock<IRqlGrammarIndex>(MockBehavior.Strict);

            this.dispatcher = new RqlToolDispatcher(this.repository.Object, this.validator.Object, this.grammarIndex.Object);
        }

        private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

        private static JsonElement NoArgs() => JsonDocument.Parse("{}").RootElement;

        [Test]
        public void Descriptors_ContainsAllSevenTools()
        {
            var names = this.dispatcher.Descriptors.Select(d => d.Name).ToArray();

            Assert.That(names, Is.EquivalentTo(new[]
                {
                    "list_schemas",
                    "get_schema",
                    "list_routes",
                    "get_route",
                    "validate_query",
                    "list_rql_topics",
                    "get_rql_syntax"
                }));
        }

        [Test]
        public void Descriptors_EachToolHasValidJsonSchemaParameters()
        {
            foreach (var descriptor in this.dispatcher.Descriptors)
            {
                Action action = () => JsonDocument.Parse(descriptor.ParametersJsonSchema).Dispose();

                Assert.DoesNotThrow(action, $"Tool '{descriptor.Name}' has invalid JSON schema parameters");
            }
        }

        [Test]
        public void Dispatch_UnknownToolName_ReturnsErrorWithAvailableList()
        {
            var result = this.dispatcher.Dispatch("frobnicate", NoArgs());

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetProperty("error").GetString(), Does.Contain("Unknown tool 'frobnicate'"));
            Assert.That(doc.RootElement.GetProperty("error").GetString(), Does.Contain("list_schemas"));
        }

        [Test]
        public void Dispatch_NullOrEmptyToolName_ReturnsError()
        {
            using var doc = JsonDocument.Parse(this.dispatcher.Dispatch(string.Empty, NoArgs()));

            Assert.That(doc.RootElement.GetProperty("error").GetString(), Does.Contain("Tool name is required"));
        }

        // -------------------- list_schemas --------------------

        [Test]
        public void ListSchemas_NoFilter_PassesNullToRepository()
        {
            this.repository
                .Setup(r => r.ListSchemas(null))
                .Returns(new[]
                    {
                        new ClassDefinition { ClassName = "Employee", Description = "An employee." }
                    });

            var result = this.dispatcher.Dispatch("list_schemas", NoArgs());

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(1));
            Assert.That(doc.RootElement[0].GetProperty("Name").GetString(), Is.EqualTo("Employee"));
        }

        [Test]
        public void ListSchemas_WithFilter_ForwardsFilterToRepository()
        {
            this.repository
                .Setup(r => r.ListSchemas("Emp"))
                .Returns(new[]
                    {
                        new ClassDefinition { ClassName = "Employee" },
                        new ClassDefinition { ClassName = "Employer" }
                    });

            var result = this.dispatcher.Dispatch("list_schemas", Args(@"{""filter"":""Emp""}"));

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(2));
        }

        // -------------------- get_schema --------------------

        [Test]
        public void GetSchema_MissingTypeName_ReturnsError()
        {
            using var doc = JsonDocument.Parse(this.dispatcher.Dispatch("get_schema", NoArgs()));

            Assert.That(doc.RootElement.GetProperty("error").GetString(), Does.Contain("typeName"));
        }

        [Test]
        public void GetSchema_KnownTypeName_ReturnsFullDto()
        {
            this.repository
                .Setup(r => r.GetSchema("Employee"))
                .Returns(new ClassDefinition
                    {
                        ClassName = "Employee",
                        Description = "An employee.",
                        Properties = new List<PropertyDefinition>
                            {
                                new PropertyDefinition { Name = "Surname", Type = "string", Description = "" }
                            }
                    });

            var result = this.dispatcher.Dispatch("get_schema", Args(@"{""typeName"":""Employee""}"));

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetProperty("Name").GetString(), Is.EqualTo("Employee"));
            Assert.That(doc.RootElement.GetProperty("Properties").GetArrayLength(), Is.EqualTo(1));
        }

        [Test]
        public void GetSchema_UnknownTypeName_ReturnsJsonNull()
        {
            this.repository
                .Setup(r => r.GetSchema("Nope"))
                .Returns((ClassDefinition?)null);

            var result = this.dispatcher.Dispatch("get_schema", Args(@"{""typeName"":""Nope""}"));

            Assert.That(result, Is.EqualTo("null"));
        }

        // -------------------- list_routes --------------------

        [Test]
        public void ListRoutes_AllFilters_ApplyAndedFiltering()
        {
            this.repository
                .Setup(r => r.GetRouteDefinitions())
                .Returns(new[]
                    {
                        new RouteDefinition { ClassName = "A", Verb = "GET",  RouteSignature = "/Employer/{id}", Tags = new List<string> { "Employer" } },
                        new RouteDefinition { ClassName = "B", Verb = "POST", RouteSignature = "/Employer/{id}/Employee", Tags = new List<string> { "Employee" } },
                        new RouteDefinition { ClassName = "C", Verb = "GET",  RouteSignature = "/Employer/{id}/Employee/{eId}", Tags = new List<string> { "Employee", "Reports" } },
                        new RouteDefinition { ClassName = "D", Verb = "GET",  RouteSignature = "/PayRun/{prId}", Tags = new List<string> { "PayRun" } }
                    });

            var result = this.dispatcher.Dispatch(
                "list_routes",
                Args(@"{""filter"":""Employee"",""verb"":""GET"",""tag"":""Reports""}"));

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(1));
            Assert.That(doc.RootElement[0].GetProperty("ClassName").GetString(), Is.EqualTo("C"));
        }

        [Test]
        public void ListRoutes_NoFilters_ReturnsAll()
        {
            this.repository
                .Setup(r => r.GetRouteDefinitions())
                .Returns(new[] { new RouteDefinition { ClassName = "A", RouteSignature = "/x" } });

            var result = this.dispatcher.Dispatch("list_routes", NoArgs());

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(1));
        }

        // -------------------- get_route --------------------

        [Test]
        public void GetRoute_MissingClassName_ReturnsError()
        {
            using var doc = JsonDocument.Parse(this.dispatcher.Dispatch("get_route", NoArgs()));

            Assert.That(doc.RootElement.GetProperty("error").GetString(), Does.Contain("className"));
        }

        [Test]
        public void GetRoute_UnknownClassName_ReturnsJsonNull()
        {
            this.repository
                .Setup(r => r.GetRouteDefinitions())
                .Returns(new[] { new RouteDefinition { ClassName = "GetEmployeeRoute" } });

            var result = this.dispatcher.Dispatch("get_route", Args(@"{""className"":""DoesNotExist""}"));

            Assert.That(result, Is.EqualTo("null"));
        }

        [Test]
        public void GetRoute_KnownClassName_ReturnsFullDto()
        {
            this.repository
                .Setup(r => r.GetRouteDefinitions())
                .Returns(new[]
                    {
                        new RouteDefinition
                            {
                                ClassName = "GetEmployeeRoute",
                                Verb = "GET",
                                RouteSignature = "/Employer/{id}/Employee/{eId}",
                                OperationId = "GetEmployee",
                                Summary = "Fetch an employee",
                                Description = "Returns the full employee entity.",
                                ResponseType = "Employee",
                                Tags = new List<string> { "Employee" }
                            }
                    });

            var result = this.dispatcher.Dispatch("get_route", Args(@"{""className"":""GetEmployeeRoute""}"));

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetProperty("ClassName").GetString(), Is.EqualTo("GetEmployeeRoute"));
            Assert.That(doc.RootElement.GetProperty("ResponseType").GetString(), Is.EqualTo("Employee"));
        }

        // -------------------- validate_query --------------------

        [Test]
        public void ValidateQuery_MissingXml_ReturnsError()
        {
            using var doc = JsonDocument.Parse(this.dispatcher.Dispatch("validate_query", NoArgs()));

            Assert.That(doc.RootElement.GetProperty("error").GetString(), Does.Contain("xml"));
        }

        [Test]
        public void ValidateQuery_ForwardsXmlToValidator_AndSerialisesResult()
        {
            this.validator
                .Setup(v => v.Validate("<Query/>"))
                .Returns(new ValidationResult
                    {
                        Diagnostics = new List<ValidationDiagnostic>
                            {
                                new ValidationDiagnostic
                                    {
                                        Severity = ValidationSeverity.Error,
                                        Code = "XsdValidation",
                                        Message = "missing RootNodeName",
                                        Line = 1,
                                        Column = 8
                                    }
                            }
                    });

            var result = this.dispatcher.Dispatch("validate_query", Args(@"{""xml"":""<Query/>""}"));

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetProperty("IsValid").GetBoolean(), Is.False);
            Assert.That(doc.RootElement.GetProperty("Diagnostics").GetArrayLength(), Is.EqualTo(1));
            Assert.That(doc.RootElement.GetProperty("Diagnostics")[0].GetProperty("Severity").GetString(), Is.EqualTo("Error"));
        }

        // -------------------- list_rql_topics --------------------

        [Test]
        public void ListRqlTopics_ReturnsAllTopicsFromIndex()
        {
            this.grammarIndex
                .SetupGet(g => g.Topics)
                .Returns(new[]
                    {
                        new RqlGrammarTopic("filters", "Filters"),
                        new RqlGrammarTopic("ordering", "Ordering")
                    });

            var result = this.dispatcher.Dispatch("list_rql_topics", NoArgs());

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(2));
            Assert.That(doc.RootElement[0].GetProperty("slug").GetString(), Is.EqualTo("filters"));
            Assert.That(doc.RootElement[0].GetProperty("title").GetString(), Is.EqualTo("Filters"));
        }

        // -------------------- get_rql_syntax --------------------

        [Test]
        public void GetRqlSyntax_MissingTopic_ReturnsError()
        {
            using var doc = JsonDocument.Parse(this.dispatcher.Dispatch("get_rql_syntax", NoArgs()));

            Assert.That(doc.RootElement.GetProperty("error").GetString(), Does.Contain("topic"));
        }

        [Test]
        public void GetRqlSyntax_KnownTopic_ReturnsTopicAndContent()
        {
            this.grammarIndex.Setup(g => g.GetTopic("filters")).Returns("## Filters\n\nBody.");

            var result = this.dispatcher.Dispatch("get_rql_syntax", Args(@"{""topic"":""filters""}"));

            using var doc = JsonDocument.Parse(result);
            Assert.That(doc.RootElement.GetProperty("topic").GetString(), Is.EqualTo("filters"));
            Assert.That(doc.RootElement.GetProperty("content").GetString(), Is.EqualTo("## Filters\n\nBody."));
        }

        [Test]
        public void GetRqlSyntax_UnknownTopic_ReturnsErrorWithAvailableList()
        {
            this.grammarIndex.Setup(g => g.GetTopic("zzz")).Returns((string?)null);
            this.grammarIndex
                .SetupGet(g => g.Topics)
                .Returns(new[] { new RqlGrammarTopic("filters", "Filters") });

            var result = this.dispatcher.Dispatch("get_rql_syntax", Args(@"{""topic"":""zzz""}"));

            using var doc = JsonDocument.Parse(result);
            var error = doc.RootElement.GetProperty("error").GetString();
            Assert.That(error, Does.Contain("Unknown topic 'zzz'"));
            Assert.That(error, Does.Contain("'filters'"));
        }
    }
}
