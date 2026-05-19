namespace PayRunIO.RqlAssistant.Service.Tests
{
    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Models;

    [TestFixture]
    public class QueryValidatorTests
    {
        private QueryValidator validator = null!;

        private const string ValidQuery = """
            <Query>
                <RootNodeName>Report</RootNodeName>
                <Groups>
                    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/ER001/Employees" />
                </Groups>
            </Query>
            """;

        [SetUp]
        public void SetUp()
        {
            this.validator = new QueryValidator();
        }

        [Test]
        public void Validate_WithValidQuery_ReturnsIsValidTrueAndNoDiagnostics()
        {
            var result = this.validator.Validate(ValidQuery);

            Assert.That(result.IsValid, Is.True, "Expected the well-formed sample query to validate.");
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void Validate_WithEmptyInput_ReturnsEmptyInputError()
        {
            var result = this.validator.Validate(string.Empty);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("EmptyInput"));
            Assert.That(result.Diagnostics[0].Severity, Is.EqualTo(ValidationSeverity.Error));
        }

        [Test]
        public void Validate_WithMalformedXml_ReportsMalformedXmlWithLineInfo()
        {
            const string malformed = "<Query><RootNodeName>Report</RootNodeName"; // unclosed tag

            var result = this.validator.Validate(malformed);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Any(d => d.Code == "MalformedXml"), Is.True);
            var diag = result.Diagnostics.First(d => d.Code == "MalformedXml");
            Assert.That(diag.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(diag.Line, Is.GreaterThan(0));
        }

        [Test]
        public void Validate_WithMissingRootNodeName_ReportsXsdError()
        {
            const string missingRoot = """
                <Query>
                    <Groups>
                        <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/ER001/Employees" />
                    </Groups>
                </Query>
                """;

            var result = this.validator.Validate(missingRoot);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Any(d => d.Code == "XsdValidation"), Is.True,
                "Expected an XSD validation error for the missing RootNodeName element.");
        }

        [Test]
        public void Validate_WithUnknownElement_ReportsXsdError()
        {
            const string unknownElement = """
                <Query>
                    <RootNodeName>Report</RootNodeName>
                    <Frobnicator />
                    <Groups>
                        <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/ER001/Employees" />
                    </Groups>
                </Query>
                """;

            var result = this.validator.Validate(unknownElement);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Diagnostics.Any(d => d.Code == "XsdValidation"), Is.True);
        }

        [Test]
        public void Validate_CalledTwice_ReusesCompiledSchemaSet()
        {
            var first = this.validator.Validate(ValidQuery);
            var second = this.validator.Validate(ValidQuery);

            Assert.That(first.IsValid, Is.True);
            Assert.That(second.IsValid, Is.True);
        }
    }
}
