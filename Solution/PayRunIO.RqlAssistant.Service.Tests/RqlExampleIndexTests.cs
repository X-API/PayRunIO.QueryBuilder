namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Linq;
    using System.Text.RegularExpressions;

    using PayRunIO.RqlAssistant.Service;

    [TestFixture]
    public class RqlExampleIndexTests
    {
        private RqlExampleIndex index = null!;

        [SetUp]
        public void SetUp()
        {
            this.index = new RqlExampleIndex();
        }

        [Test]
        public void Examples_BankIsNotEmpty()
        {
            Assert.That(this.index.Examples, Is.Not.Empty);
        }

        [Test]
        public void Examples_EveryExampleHasRequestTagsAndXml()
        {
            foreach (var example in this.index.Examples)
            {
                Assert.That(example.Request, Is.Not.Empty, $"Example '{example.Slug}' is missing its **Request:** line.");
                Assert.That(example.Tags, Is.Not.Empty, $"Example '{example.Slug}' is missing its **Tags:** line.");
                Assert.That(example.Body, Does.Contain("```xml"), $"Example '{example.Slug}' has no XML code block.");
            }
        }

        [Test]
        public void Examples_SlugsAreUnique()
        {
            var slugs = this.index.Examples.Select(e => e.Slug).ToArray();

            Assert.That(slugs, Is.Unique);
        }

        [Test]
        public void GetExample_KnownSlug_ReturnsBodyStartingWithHeading()
        {
            var first = this.index.Examples.First();

            var fetched = this.index.GetExample(first.Slug);

            Assert.That(fetched, Is.Not.Null);
            Assert.That(fetched!.Body, Does.StartWith("## "));
        }

        [Test]
        public void GetExample_IsCaseInsensitive()
        {
            var first = this.index.Examples.First();

            Assert.That(this.index.GetExample(first.Slug.ToUpperInvariant()), Is.EqualTo(fetchedBySlug()));

            RqlExample? fetchedBySlug() => this.index.GetExample(first.Slug);
        }

        [Test]
        public void GetExample_UnknownSlug_ReturnsNull()
        {
            Assert.That(this.index.GetExample("nonsense-example"), Is.Null);
        }

        [Test]
        public void GetExample_NullOrWhitespace_ReturnsNull()
        {
            Assert.That(this.index.GetExample(string.Empty), Is.Null);
            Assert.That(this.index.GetExample("   "), Is.Null);
        }

        [Test]
        public void FilterExamples_SingleTerm_MatchesTagsAndTitle()
        {
            var matches = RqlToolDispatcher.FilterExamples(this.index.Examples, "pension").ToArray();

            Assert.That(matches, Is.Not.Empty);
            Assert.That(
                matches.All(e =>
                    e.Slug.Contains("pension", StringComparison.OrdinalIgnoreCase)
                    || e.Title.Contains("pension", StringComparison.OrdinalIgnoreCase)
                    || e.Request.Contains("pension", StringComparison.OrdinalIgnoreCase)
                    || e.Tags.Any(t => t.Contains("pension", StringComparison.OrdinalIgnoreCase))),
                Is.True);
        }

        [Test]
        public void FilterExamples_MultipleTerms_AreAnded()
        {
            var all = this.index.Examples;

            var broad = RqlToolDispatcher.FilterExamples(all, "employee").Count();
            var narrow = RqlToolDispatcher.FilterExamples(all, "employee pension").Count();

            Assert.That(narrow, Is.LessThanOrEqualTo(broad));
            Assert.That(narrow, Is.GreaterThan(0));
        }

        [Test]
        public void FilterExamples_NullOrEmptyFilter_ReturnsAll()
        {
            Assert.That(RqlToolDispatcher.FilterExamples(this.index.Examples, null), Is.EquivalentTo(this.index.Examples));
            Assert.That(RqlToolDispatcher.FilterExamples(this.index.Examples, "  "), Is.EquivalentTo(this.index.Examples));
        }

        [Test]
        public void Examples_IncludeCommonPayrollRequests()
        {
            var slugs = this.index.Examples.Select(e => e.Slug).ToArray();

            Assert.That(slugs, Has.Member("net-pay-per-employee-for-a-payment-date"));
            Assert.That(slugs, Has.Member("tabular-gross-to-net-report"));
            Assert.That(slugs, Has.Member("employee-list-with-selected-properties"));
        }
    }

    /// <summary>
    /// Guarantees the example bank cannot rot: every XML code block in every example must
    /// validate against QuerySchema.xsd. A schema or route change that invalidates an example
    /// fails this fixture, naming the example and the diagnostics.
    /// </summary>
    [TestFixture]
    public class RqlExamplesValidationTests
    {
        private static readonly Regex XmlBlock = new Regex("```xml\\s*(?<xml>[\\s\\S]*?)\\s*```", RegexOptions.Compiled);

        private static IEnumerable<TestCaseData> ExampleXmlBlocks()
        {
            var index = new RqlExampleIndex();

            foreach (var example in index.Examples)
            {
                var blocks = XmlBlock.Matches(example.Body);

                for (var i = 0; i < blocks.Count; i++)
                {
                    yield return new TestCaseData(example.Slug, blocks[i].Groups["xml"].Value)
                        .SetName($"ExampleXml_IsSchemaValid({example.Slug}[{i}])");
                }
            }
        }

        [TestCaseSource(nameof(ExampleXmlBlocks))]
        public void ExampleXml_IsSchemaValid(string slug, string xml)
        {
            var validator = new QueryValidator();

            var result = validator.Validate(xml);

            var diagnostics = string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(d => $"[{d.Severity}] line {d.Line}, col {d.Column}: {d.Code} — {d.Message}"));

            Assert.That(result.IsValid, Is.True, $"Example '{slug}' failed XSD validation:{Environment.NewLine}{diagnostics}");
        }

        [TestCaseSource(nameof(ExampleXmlBlocks))]
        public void ExampleXml_IsLintClean(string slug, string xml)
        {
            var linter = new RqlSemanticLinter(new DocumentRepository());

            var findings = linter.Lint(xml);

            var diagnostics = string.Join(
                Environment.NewLine,
                findings.Select(d => $"[{d.Severity}] line {d.Line}, col {d.Column}: {d.Code} — {d.Message}"));

            Assert.That(findings, Is.Empty, $"Example '{slug}' has semantic lint findings:{Environment.NewLine}{diagnostics}");
        }
    }
}
