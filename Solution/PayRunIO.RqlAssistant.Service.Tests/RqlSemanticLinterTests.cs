namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Linq;

    using PayRunIO.RqlAssistant.Service;

    /// <summary>
    /// Exercises the semantic linter against the real embedded routes.json / dtos.json so the
    /// checks are proven against production data rather than fixtures.
    /// </summary>
    [TestFixture]
    public class RqlSemanticLinterTests
    {
        private RqlSemanticLinter linter = null!;

        [SetUp]
        public void SetUp()
        {
            this.linter = new RqlSemanticLinter(new DocumentRepository());
        }

        private static string Query(string body) =>
            "<Query xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
            + "<RootNodeName>Test</RootNodeName>"
            + body
            + "</Query>";

        [Test]
        public void Lint_KnownRouteSelector_NoDiagnostics()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\"><Output xsi:type=\"RenderEntity\" /></Group></Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
        }

        [Test]
        public void Lint_UnknownRouteSelector_WarnsUnknownRoute()
        {
            var xml = Query("<Groups><Group Selector=\"/Frobnicators\"><Output xsi:type=\"RenderEntity\" /></Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownRoute"));
        }

        [Test]
        public void Lint_WildcardSegment_MatchesRouteParameter()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/*/PayLines\"><Output xsi:type=\"Sum\" Name=\"Total\" Property=\"Value\" /></Group></Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
        }

        [Test]
        public void Lint_SelectorNotStartingWithSlash_IsSkipped()
        {
            var xml = Query("<Groups><Group Selector=\"[FullPath]\"><Output xsi:type=\"RenderEntity\" /></Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.No.Member("UnknownRoute"));
        }

        [Test]
        public void Lint_UnknownPropertyOnAnySchema_WarnsUnknownProperty()
        {
            var xml = Query(
                "<Groups><Group Selector=\"/Employers\">"
                + "<Output xsi:type=\"RenderProperty\" Name=\"X\" Property=\"NotARealPropertyName\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownProperty"));
        }

        [Test]
        public void Lint_PropertyValidForOfTypeSchema_NoDiagnostics()
        {
            var xml = Query(
                "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines\">"
                + "<Filter xsi:type=\"OfType\" Value=\"PayLineNi\" />"
                + "<Output xsi:type=\"Sum\" Name=\"ErNi\" Property=\"EmployerNI\" Negate=\"true\" />"
                + "</Group></Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
        }

        [Test]
        public void Lint_PropertyInvalidForOfTypeSchema_WarnsUnknownProperty()
        {
            var xml = Query(
                "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines\">"
                + "<Filter xsi:type=\"OfType\" Value=\"PayLineTax\" />"
                + "<Output xsi:type=\"Sum\" Name=\"ErNi\" Property=\"EmployerNI\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownProperty"));
        }

        [Test]
        public void Lint_UnknownOfTypeValue_WarnsUnknownEntityType()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/*/PayLines\">"
                + "<Filter xsi:type=\"OfType\" Value=\"PayLineFrobnicator\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownEntityType"));
        }

        [Test]
        public void Lint_VariableUsedButNeverAssigned_WarnsUnassignedVariable()
        {
            var xml = Query(
                "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\"><Output xsi:type=\"RenderEntity\" /></Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnassignedVariable"));
            Assert.That(diagnostics.Single(d => d.Code == "UnassignedVariable").Message, Does.Contain("[EmployerKey]"));
        }

        [Test]
        public void Lint_VariableAssignments_AreRecognised()
        {
            // [EmployerKey] via <Variables>, [EmployeeKey] via UniqueKeyVariable,
            // [NetPay] via a Variable output, [LoopVariable] via a LoopExpression.
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups>"
                + "<Group Selector=\"/Employer/[EmployerKey]/Employees\" UniqueKeyVariable=\"[EmployeeKey]\">"
                + "<Output xsi:type=\"RenderValue\" Output=\"Variable\" Name=\"[NetPay]\" Value=\"0\" />"
                + "<Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines\">"
                + "<Output xsi:type=\"Sum\" Output=\"Variable\" Name=\"[NetPay]\" Property=\"Value\" />"
                + "</Group>"
                + "<Group>"
                + "<Output xsi:type=\"RenderValue\" Name=\"NetPay\" Value=\"[NetPay]\" Format=\"0.00\" />"
                + "</Group>"
                + "</Group>"
                + "<Group GroupName=\"Loop\" LoopExpression=\"Range:1-3\">"
                + "<Output xsi:type=\"RenderValue\" Name=\"Value\" Value=\"[LoopVariable]\" />"
                + "</Group>"
                + "</Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
        }

        [Test]
        public void Lint_RequiredVariables_CountAsAssigned()
        {
            var xml = Query(
                "<Required><Variable>[EmployerKey]</Variable></Required>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\"><Output xsi:type=\"RenderEntity\" /></Group></Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
        }

        [Test]
        public void Lint_MalformedXml_ReturnsNoDiagnostics()
        {
            Assert.That(this.linter.Lint("<Query><oops"), Is.Empty);
        }

        [Test]
        public void Lint_AllDiagnosticsAreWarnings()
        {
            var xml = Query(
                "<Groups><Group Selector=\"/Frobnicators\">"
                + "<Output xsi:type=\"RenderProperty\" Name=\"X\" Property=\"NotARealPropertyName\" />"
                + "<Output xsi:type=\"RenderValue\" Name=\"Y\" Value=\"[Nowhere]\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics, Is.Not.Empty);
            Assert.That(diagnostics.All(d => d.Severity == Service.Models.ValidationSeverity.Warning), Is.True);
        }
    }
}
