namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Linq;

    using PayRunIO.RqlAssistant.Service;

    /// <summary>
    /// Guards the sub element documentation surface. The generated schema is a flat per-class
    /// property list, which hides the fact that a property typed as another schema can be traversed
    /// with a dotted path. An agent asked for an employee's sort code scanned Employee's properties,
    /// found no "SortCode", and told the user the field did not exist — when Employee.BankAccount
    /// leads straight to it. These tests pin the guidance that closes that gap.
    /// </summary>
    [TestFixture]
    public class SubElementDocsTests
    {
        private DocumentRepository repository = null!;

        [SetUp]
        public void SetUp()
        {
            this.repository = new DocumentRepository();
        }

        /// <summary>
        /// The original reported failure, pinned end to end: reading only Employee's schema must be
        /// enough to discover that the sort code lives on the BankAccount sub element.
        /// </summary>
        [Test]
        public void GetSchema_Employee_PointsAtBankAccountSortCode()
        {
            var employee = this.repository.GetSchema("Employee");

            Assert.That(employee, Is.Not.Null);

            var bankAccount = employee!.Properties.Single(p => p.Name == "BankAccount");

            Assert.That(bankAccount.Description, Does.Contain("Sub element"));
            Assert.That(bankAccount.Description, Does.Contain("BankAccount.<Property>"));
            Assert.That(bankAccount.Description, Does.Contain("SortCode"));
        }

        /// <summary>
        /// The annotation must enumerate the reachable property names. Naming the target type alone
        /// would still require a second lookup the agent has no reason to know it needs.
        /// </summary>
        [Test]
        public void GetSchema_SubElementProperty_ListsReachablePropertyNames()
        {
            var employee = this.repository.GetSchema("Employee");
            var address = employee!.Properties.Single(p => p.Name == "Address");

            Assert.That(address.Description, Does.Contain("Postcode"));
            Assert.That(address.Description, Does.Contain("get_schema('Address')"));
        }

        /// <summary>
        /// Collections are a different access pattern — a nested group selects them, dotting through
        /// does not work — so they must not be labelled with the dotted-path form.
        /// </summary>
        [Test]
        public void GetSchema_CollectionProperty_DoesNotAdvertiseDottedPath()
        {
            var employee = this.repository.GetSchema("Employee");
            var groups = employee!.Properties.Single(p => p.Name == "Groups");

            Assert.That(groups.Description, Does.Contain("Sub element collection"));
            Assert.That(groups.Description, Does.Not.Contain("Groups.<Property>"));
        }

        /// <summary>
        /// Not every complex type is present in the generated schema (WorkingWeek, for one). With no
        /// target class there are no property names to advertise, so the property must be left alone
        /// rather than annotated with an empty or unresolvable traversal hint.
        /// </summary>
        [Test]
        public void GetSchema_UnresolvableComplexType_IsNotAnnotated()
        {
            var employee = this.repository.GetSchema("Employee");
            var shiftPattern = employee!.Properties.Single(p => p.Name == "ShiftPattern");

            Assert.That(this.repository.GetSchema("WorkingWeek"), Is.Null);
            Assert.That(shiftPattern.Description, Does.Not.Contain("Sub element"));
        }

        /// <summary>
        /// Scalars must be left untouched, so the annotation stays a reliable traversal signal
        /// rather than noise attached to every property.
        /// </summary>
        [Test]
        public void GetSchema_ScalarProperty_IsNotAnnotated()
        {
            var employee = this.repository.GetSchema("Employee");
            var code = employee!.Properties.Single(p => p.Name == "Code");

            Assert.That(code.Description, Does.Not.Contain("Sub element"));
        }

        /// <summary>
        /// MetaData is dotted into like a sub element, but the name after the dot comes from the data.
        /// It must not be described with the generic form — listing Items/AllItemNames as "reachable"
        /// would teach exactly the collection-navigation mistake the meta data guidance forbids.
        /// </summary>
        [Test]
        public void GetSchema_MetaDataProperty_KeepsPseudoPropertyGuidance()
        {
            var employee = this.repository.GetSchema("Employee");
            var metaData = employee!.Properties.Single(p => p.Name == "MetaData");

            Assert.That(metaData.Description, Does.Not.Contain("MetaData.<Property>"));
            Assert.That(metaData.Description, Does.Not.Contain("MetaData exposes:"));

            // It should still point somewhere useful rather than being left blank.
            Assert.That(metaData.Description, Does.Contain("item name"));
            Assert.That(metaData.Description, Does.Contain("get_rql_syntax('meta-data')"));
        }

        [Test]
        public void GrammarIndex_ExposesSubElementsTopic()
        {
            var index = new RqlGrammarIndex();

            Assert.That(index.Topics.Select(t => t.Slug), Has.Member("sub-elements-and-property-paths"));

            var body = index.GetTopic("sub-elements-and-property-paths");

            Assert.That(body, Is.Not.Null);

            // The worked example that motivated the topic.
            Assert.That(body, Does.Contain("BankAccount.SortCode"));

            // The collection boundary is the rule most likely to be over-generalised.
            Assert.That(body, Does.Contain("Collection<"));
        }

        /// <summary>
        /// Every RQL query embedded in the sub element documentation must survive the project's own
        /// validation and linting, so the topic cannot teach a pattern the tools then reject.
        /// </summary>
        [Test]
        public void SubElementTopic_EmbeddedQueriesValidateAndLintClean()
        {
            var index = new RqlGrammarIndex();
            var validator = new QueryValidator();
            var linter = new RqlSemanticLinter(this.repository);

            var body = index.GetTopic("sub-elements-and-property-paths")!;

            var queries = ExtractQueries(body);

            Assert.That(queries, Is.Not.Empty, "The topic should carry worked query examples.");

            foreach (var xml in queries)
            {
                var result = validator.Validate(xml);

                Assert.That(result.IsValid, Is.True, $"A topic query failed schema validation:\n{xml}");

                var warnings = linter.Lint(xml).ToArray();

                Assert.That(
                    warnings,
                    Is.Empty,
                    $"A topic query produced linter warnings:\n{string.Join("\n", warnings.Select(w => $"  [{w.Code}] {w.Message}"))}\n{xml}");
            }
        }

        /// <summary>
        /// Pulls the fenced xml blocks that contain a full query document. Fragments illustrating a
        /// single element are deliberately skipped — they are not independently validatable.
        /// </summary>
        private static IReadOnlyList<string> ExtractQueries(string markdown)
        {
            var queries = new List<string>();
            var searchFrom = 0;

            while (true)
            {
                var start = markdown.IndexOf("```xml", searchFrom, StringComparison.Ordinal);

                if (start < 0)
                {
                    break;
                }

                var end = markdown.IndexOf("```", start + 6, StringComparison.Ordinal);

                if (end < 0)
                {
                    break;
                }

                var block = markdown.Substring(start + 6, end - start - 6).Trim();
                searchFrom = end + 3;

                if (block.StartsWith("<Query", StringComparison.Ordinal))
                {
                    queries.Add(block);
                }
            }

            return queries;
        }
    }
}
