namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Linq;

    using PayRunIO.RqlAssistant.Service;

    [TestFixture]
    public class RqlGrammarIndexTests
    {
        private RqlGrammarIndex index = null!;

        [SetUp]
        public void SetUp()
        {
            this.index = new RqlGrammarIndex();
        }

        [Test]
        public void Topics_ExcludesTableOfContents()
        {
            Assert.That(this.index.Topics.Any(t => t.Slug == "table-of-contents"), Is.False);
        }

        [Test]
        public void Topics_IncludesCoreGrammarSections()
        {
            var slugs = this.index.Topics.Select(t => t.Slug).ToArray();

            Assert.That(slugs, Has.Member("creating-your-first-query"));
            Assert.That(slugs, Has.Member("filters"));
            Assert.That(slugs, Has.Member("ordering"));
            Assert.That(slugs, Has.Member("conditions-and-conditional-group-logic"));
            Assert.That(slugs, Has.Member("outputs"));
            Assert.That(slugs, Has.Member("variables"));
            Assert.That(slugs, Has.Member("loop-expressions"));
        }

        [Test]
        public void GetTopic_KnownSlug_ReturnsBodyStartingWithHeading()
        {
            var body = this.index.GetTopic("filters");

            Assert.That(body, Is.Not.Null);
            Assert.That(body, Does.StartWith("## Filters"));
        }

        [Test]
        public void GetTopic_IsCaseInsensitive()
        {
            var lower = this.index.GetTopic("filters");
            var mixed = this.index.GetTopic("Filters");

            Assert.That(mixed, Is.EqualTo(lower));
        }

        [Test]
        public void GetTopic_UnknownSlug_ReturnsNull()
        {
            Assert.That(this.index.GetTopic("nonsense-topic"), Is.Null);
        }

        [Test]
        public void GetTopic_NullOrWhitespace_ReturnsNull()
        {
            Assert.That(this.index.GetTopic(string.Empty), Is.Null);
            Assert.That(this.index.GetTopic("   "), Is.Null);
        }

        [Test]
        public void Slugify_DropsPunctuationAndLowercases()
        {
            Assert.That(RqlGrammarIndex.Slugify("Filters  "), Is.EqualTo("filters"));
            Assert.That(RqlGrammarIndex.Slugify("Conditions and Conditional Group Logic"), Is.EqualTo("conditions-and-conditional-group-logic"));
            Assert.That(RqlGrammarIndex.Slugify("Advanced Features Pt.1"), Is.EqualTo("advanced-features-pt1"));
            Assert.That(RqlGrammarIndex.Slugify("📚 Table of Contents"), Is.EqualTo("table-of-contents"));
        }
    }
}
