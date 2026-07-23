namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Linq;

    using PayRunIO.RqlAssistant.Service;

    /// <summary>
    /// Guards the meta data documentation surface. Meta data is the one construct whose RQL access
    /// pattern cannot be inferred from the generated schema, so the grammar topic and worked
    /// examples are load-bearing rather than merely nice to have.
    /// </summary>
    [TestFixture]
    public class MetaDataDocsTests
    {
        [Test]
        public void GrammarIndex_ExposesMetaDataTopic()
        {
            var index = new RqlGrammarIndex();

            Assert.That(index.Topics.Select(t => t.Slug), Has.Member("meta-data"));

            var body = index.GetTopic("meta-data");

            Assert.That(body, Is.Not.Null);
            Assert.That(body, Does.Contain("MetaData.AllItemNames"));
            Assert.That(body, Does.Contain("MetaData.[LoopVariable]"));

            // The two invalid forms must be called out explicitly, not merely omitted.
            Assert.That(body, Does.Contain("MetaData.Items CONTAINS"));
            Assert.That(body, Does.Contain("cannot be used in a group `Predicate`"));
        }

        [Test]
        public void ExampleIndex_ExposesMetaDataExamples()
        {
            var index = new RqlExampleIndex();
            var metaDataExamples = index.Examples
                .Where(e => e.Tags.Contains("meta-data"))
                .ToArray();

            Assert.That(metaDataExamples, Has.Length.EqualTo(2));
            Assert.That(metaDataExamples.All(e => e.Request.Length > 0), Is.True);
            Assert.That(
                metaDataExamples.Any(e => e.Body.Contains("MetaData.AllItemNames")),
                Is.True);
        }

        /// <summary>
        /// Every RQL query embedded in the meta data documentation must survive the project's own
        /// validation and linting, so the examples cannot teach a pattern the tools then reject.
        /// </summary>
        [Test]
        public void MetaDataExamples_ValidateAndLintClean()
        {
            var repository = new DocumentRepository();
            var validator = new QueryValidator();
            var linter = new RqlSemanticLinter(repository);
            var index = new RqlExampleIndex();

            foreach (var example in index.Examples.Where(e => e.Tags.Contains("meta-data")))
            {
                var start = example.Body.IndexOf("```xml", System.StringComparison.Ordinal);
                var end = example.Body.IndexOf("```", start + 6, System.StringComparison.Ordinal);
                var xml = example.Body.Substring(start + 6, end - start - 6).Trim();

                var result = validator.Validate(xml);

                Assert.That(result.IsValid, Is.True, $"'{example.Slug}' failed schema validation.");
                Assert.That(
                    linter.Lint(xml),
                    Is.Empty,
                    $"'{example.Slug}' produced linter warnings.");
            }
        }
    }
}
