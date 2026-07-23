namespace PayRunIO.RqlAssistant.Service.Tests
{
    [TestFixture]
    public class DocumentRepositoryTests
    {
        private DocumentRepository repository = null!;

        [SetUp]
        public void SetUp()
        {
            this.repository = new DocumentRepository();
        }

        [Test]
        public void GetSchema_WithKnownTypeName_ReturnsSchema()
        {
            var schema = this.repository.GetSchema("Employee");

            Assert.That(schema, Is.Not.Null);
            Assert.That(schema!.ClassName, Is.EqualTo("Employee"));
            Assert.That(schema.Properties, Is.Not.Empty);
        }

        [Test]
        public void GetSchema_IsCaseInsensitive()
        {
            var lower = this.repository.GetSchema("employee");
            var upper = this.repository.GetSchema("EMPLOYEE");
            var mixed = this.repository.GetSchema("EmPlOyEe");

            Assert.That(lower, Is.Not.Null);
            Assert.That(upper, Is.Not.Null);
            Assert.That(mixed, Is.Not.Null);
            Assert.That(lower!.ClassName, Is.EqualTo(upper!.ClassName));
            Assert.That(lower.ClassName, Is.EqualTo(mixed!.ClassName));
        }

        [Test]
        public void GetSchema_WithUnknownTypeName_ReturnsNull()
        {
            var schema = this.repository.GetSchema("ThisSchemaDoesNotExist");

            Assert.That(schema, Is.Null);
        }

        [Test]
        public void GetSchema_WithNullOrWhitespace_ReturnsNull()
        {
            Assert.That(this.repository.GetSchema(null!), Is.Null);
            Assert.That(this.repository.GetSchema(string.Empty), Is.Null);
            Assert.That(this.repository.GetSchema("   "), Is.Null);
        }

        [Test]
        public void ListSchemas_WithNullFilter_ReturnsAllSchemas()
        {
            var schemas = this.repository.ListSchemas().ToArray();

            Assert.That(schemas, Is.Not.Empty);
            Assert.That(schemas.Any(s => s.ClassName == "Employee"), Is.True);
        }

        [Test]
        public void ListSchemas_WithEmptyFilter_ReturnsAllSchemas()
        {
            var withNullFilter = this.repository.ListSchemas(null).Count();
            var withEmptyFilter = this.repository.ListSchemas(string.Empty).Count();
            var withWhitespaceFilter = this.repository.ListSchemas("   ").Count();

            Assert.That(withEmptyFilter, Is.EqualTo(withNullFilter));
            Assert.That(withWhitespaceFilter, Is.EqualTo(withNullFilter));
        }

        [Test]
        public void ListSchemas_WithFilter_ReturnsOnlyMatchingSchemas()
        {
            var schemas = this.repository.ListSchemas("Pay").ToArray();

            Assert.That(schemas, Is.Not.Empty);
            Assert.That(schemas.All(s => s.ClassName != null && s.ClassName.Contains("Pay", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public void ListSchemas_FilterIsCaseInsensitive()
        {
            var lower = this.repository.ListSchemas("employee").Count();
            var upper = this.repository.ListSchemas("EMPLOYEE").Count();
            var mixed = this.repository.ListSchemas("EmPlOyEe").Count();

            Assert.That(lower, Is.GreaterThan(0));
            Assert.That(lower, Is.EqualTo(upper));
            Assert.That(lower, Is.EqualTo(mixed));
        }

        [Test]
        public void ListSchemas_WithNonMatchingFilter_ReturnsEmpty()
        {
            var schemas = this.repository.ListSchemas("ZZZZ_no_such_substring_ZZZZ").ToArray();

            Assert.That(schemas, Is.Empty);
        }

        /// <summary>
        /// The generated dtos.json carries no descriptions for the MetaData members, and its
        /// Collection&lt;MetaDataItem&gt; shape actively suggests the invalid collection-navigation
        /// form. The repository injects RQL usage guidance at load time so the schema tools cannot
        /// hand an agent that misleading picture.
        /// </summary>
        [Test]
        public void GetSchema_MetaData_CarriesRqlUsageGuidance()
        {
            var schema = this.repository.GetSchema("MetaData");

            Assert.That(schema, Is.Not.Null);
            Assert.That(schema!.Description, Does.Contain("pseudo property"));

            var items = schema.Properties.Single(p => p.Name == "Items");
            var allItemNames = schema.Properties.Single(p => p.Name == "AllItemNames");

            Assert.That(items.Description, Does.Contain("NOT addressable"));
            Assert.That(allItemNames.Description, Does.Contain("comma separated"));
            Assert.That(allItemNames.Description, Does.Contain("NOT valid in a group Predicate"));
        }
    }
}
