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
        public void Lint_PropertyValidForOutputScopedOfType_NoDiagnostics()
        {
            var xml = Query(
                "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines\">"
                + "<Output xsi:type=\"Sum\" Name=\"TaxablePay\" Property=\"TaxablePay\">"
                + "<Filter xsi:type=\"OfType\" Value=\"PayLineTax\" />"
                + "</Output>"
                + "<Output xsi:type=\"Sum\" Name=\"NetPay\" Property=\"Value\" />"
                + "</Group></Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
        }

        [Test]
        public void Lint_PropertyInvalidForOutputScopedOfType_WarnsUnknownProperty()
        {
            var xml = Query(
                "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines\">"
                + "<Output xsi:type=\"Sum\" Name=\"ErNi\" Property=\"EmployerNI\">"
                + "<Filter xsi:type=\"OfType\" Value=\"PayLineTax\" />"
                + "</Output>"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownProperty"));
        }

        [Test]
        public void Lint_OutputScopedOfType_DoesNotNarrowSiblingOutputs()
        {
            var xml = Query(
                "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines\">"
                + "<Output xsi:type=\"Sum\" Name=\"TaxablePay\" Property=\"TaxablePay\">"
                + "<Filter xsi:type=\"OfType\" Value=\"PayLineTax\" />"
                + "</Output>"
                + "<Output xsi:type=\"Sum\" Name=\"Bad\" Property=\"TaxablePay\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownProperty"));
        }

        [Test]
        public void Lint_UnknownOutputScopedOfTypeValue_WarnsUnknownEntityType()
        {
            var xml = Query(
                "<Groups><Group Selector=\"/Employers\">"
                + "<Output xsi:type=\"Sum\" Name=\"X\" Property=\"Value\">"
                + "<Filter xsi:type=\"OfType\" Value=\"NotARealEntityType\" />"
                + "</Output>"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownEntityType"));
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
        public void Lint_PropertyValidForPredicateOfType_NoDiagnostics()
        {
            // An OFTYPE comparison in the Predicate narrows the entity type the same way an
            // OfType filter does: UnitsAccrued exists on PayLineHoliday, not on the PayLine base.
            var xml = Query(
                "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines\" Predicate=\"OFTYPE = 'PayLineHoliday'\">"
                + "<Output xsi:type=\"Sum\" Name=\"Accrued\" Property=\"UnitsAccrued\" />"
                + "</Group></Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
        }

        [Test]
        public void Lint_PropertyInvalidForPredicateOfType_WarnsUnknownProperty()
        {
            var xml = Query(
                "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines\" Predicate=\"OFTYPE = 'PayLineTax'\">"
                + "<Output xsi:type=\"Sum\" Name=\"ErNi\" Property=\"EmployerNI\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownProperty"));
        }

        [Test]
        public void Lint_UnknownPredicateOfTypeValue_WarnsUnknownEntityType()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employee/*/PayLines\" Predicate=\"OFTYPE = 'PayLineFrobnicator'\">"
                + "<Output xsi:type=\"Sum\" Name=\"Total\" Property=\"Value\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownEntityType"));
        }

        [Test]
        public void Lint_RenderTaxPeriodDateVariableOutput_AssignsDisplayNameVariable()
        {
            // RenderTaxPeriodDate names its target in DisplayName rather than Name; with
            // Output="Variable" that variable counts as assigned.
            var xml = Query(
                "<Variables><Variable Name=\"[TaxYear]\" Value=\"2025\" /></Variables>"
                + "<Groups>"
                + "<Group GroupName=\"Setup\">"
                + "<Output xsi:type=\"RenderTaxPeriodDate\" Output=\"Variable\" DisplayName=\"[TaxYearStart]\" TaxYear=\"[TaxYear]\" TaxPeriod=\"1\" PayFrequency=\"Monthly\" Format=\"yyyy-MM-dd\" />"
                + "</Group>"
                + "<Group GroupName=\"Render\">"
                + "<Output xsi:type=\"RenderValue\" Name=\"Start\" Value=\"[TaxYearStart]\" />"
                + "</Group>"
                + "</Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
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
        public void Lint_VariableSegmentOverRouteLiteral_WarnsUnknownRoute()
        {
            // '[RowKey]/PaySchedule' must not match '/Employees/Tag/{tagId}': variables only
            // substitute into route parameter slots, never literal segments like 'Tag'.
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\" UniqueKeyVariable=\"[RowKey]\">"
                + "<Group Selector=\"/Employer/[EmployerKey]/Employees/[RowKey]/PaySchedule\">"
                + "<Output xsi:type=\"RenderEntity\" /></Group>"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownRoute"));
        }

        [Test]
        public void Lint_LiteralFailingTypedParameterConstraint_WarnsUnknownRoute()
        {
            // '/Employer/{id}/PayRuns' is not a real route; the literal 'PayRuns' must not be
            // swallowed by the '/Employer/{id}/{effectiveDate:datetime(yyyy-MM-dd)}' catch-all.
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/PayRuns\"><Output xsi:type=\"RenderEntity\" /></Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownRoute"));
        }

        [Test]
        public void Lint_LiteralSatisfyingTypedParameterConstraint_NoWarning()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees/2024-04-06\"><Output xsi:type=\"RenderEntity\" /></Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.No.Member("UnknownRoute"));
        }

        [Test]
        public void Lint_OrderInEntityLessGroup_Warns()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Group><Order xsi:type=\"Ascending\" Property=\"LastName\" /></Group>"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("OrderInEntityLessGroup"));
        }

        [Test]
        public void Lint_FilterInEntityLessGroup_Warns()
        {
            var xml = Query(
                "<Groups><Group>"
                + "<Filter xsi:type=\"TakeFirst\" Value=\"1\" />"
                + "<Output xsi:type=\"RenderValue\" Name=\"X\" Value=\"1\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("FilterInEntityLessGroup"));
        }

        [Test]
        public void Lint_OrderInGroupWithSelector_NoWarning()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Output xsi:type=\"RenderEntity\" />"
                + "<Order xsi:type=\"Ascending\" Property=\"LastName\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.No.Member("OrderInEntityLessGroup"));
        }

        [Test]
        public void Lint_RoutePinnedScope_UnknownPropertyOnSelectedEntity_Warns()
        {
            // 'PaymentDate' is not an Employee property; the Employees route pins the scope so the
            // global property-name fallback must not mask the mistake.
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Output xsi:type=\"RenderProperty\" Name=\"col\" Property=\"PaymentDate\" />"
                + "</Group></Groups>");

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("UnknownProperty"));
        }

        [Test]
        public void Lint_RoutePinnedScope_ValidPropertyOnSelectedEntity_NoWarning()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Output xsi:type=\"RenderProperty\" Name=\"col\" Property=\"FirstName\" />"
                + "</Group></Groups>");

            Assert.That(this.linter.Lint(xml), Is.Empty);
        }

        [Test]
        public void Lint_TableQuery_CollectionRenderWithoutTakeFirst_Warns()
        {
            var xml =
                "<Query xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<RootNodeName>Table</RootNodeName>"
                + "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group GroupName=\"Rows\" ItemName=\"Row\" Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayRuns\">"
                + "<Output xsi:type=\"RenderProperty\" Name=\"col\" Property=\"PaymentDate\" />"
                + "<Order xsi:type=\"Descending\" Property=\"PaymentDate\" />"
                + "</Group>"
                + "</Group></Groups></Query>";

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.Member("CollectionRenderInTableRow"));
        }

        [Test]
        public void Lint_TableQuery_CollectionRenderWithTakeFirst_NoWarning()
        {
            var xml =
                "<Query xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<RootNodeName>Table</RootNodeName>"
                + "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group GroupName=\"Rows\" ItemName=\"Row\" Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayRuns\">"
                + "<Filter xsi:type=\"TakeFirst\" Value=\"1\" />"
                + "<Output xsi:type=\"RenderProperty\" Name=\"col\" Property=\"PaymentDate\" />"
                + "<Order xsi:type=\"Descending\" Property=\"PaymentDate\" />"
                + "</Group>"
                + "</Group></Groups></Query>";

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.No.Member("CollectionRenderInTableRow"));
        }

        [Test]
        public void Lint_TableQuery_CollectionCaptureToVariable_NoWarning()
        {
            var xml =
                "<Query xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<RootNodeName>Table</RootNodeName>"
                + "<Variables>"
                + "<Variable Name=\"[EmployerKey]\" Value=\"ER001\" />"
                + "<Variable Name=\"[EmployeeKey]\" Value=\"EE001\" />"
                + "</Variables>"
                + "<Groups><Group GroupName=\"Rows\" ItemName=\"Row\" Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Group Selector=\"/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayRuns\">"
                + "<Output xsi:type=\"RenderProperty\" Output=\"Variable\" Name=\"[LastPayment]\" Property=\"PaymentDate\" />"
                + "<Order xsi:type=\"Descending\" Property=\"PaymentDate\" />"
                + "</Group>"
                + "</Group></Groups></Query>";

            var diagnostics = this.linter.Lint(xml);

            Assert.That(diagnostics.Select(d => d.Code), Has.No.Member("CollectionRenderInTableRow"));
        }

        [Test]
        public void Lint_TableQuery_RowsGroupDirectlyUnderGroups_NoPlacementWarning()
        {
            var xml =
                "<Query xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<RootNodeName>Table</RootNodeName>"
                + "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups>"
                + "<Group GroupName=\"Headers\"><Output xsi:type=\"RenderValue\" Name=\"col\" Value=\"Code\" /></Group>"
                + "<Group GroupName=\"Rows\" ItemName=\"Row\" Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Output xsi:type=\"RenderProperty\" Output=\"Variable\" Name=\"[Code]\" Property=\"Code\" />"
                + "<Group><Output xsi:type=\"RenderValue\" Name=\"col\" Value=\"[Code]\" /></Group>"
                + "</Group>"
                + "</Groups></Query>";

            var codes = this.linter.Lint(xml).Select(d => d.Code).ToList();

            Assert.That(codes, Has.No.Member("TabularRowsNested"));
            Assert.That(codes, Has.No.Member("TabularMissingRowsGroup"));
        }

        [Test]
        public void Lint_TableQuery_RowsGroupNestedInsideNamedGroup_WarnsTabularRowsNested()
        {
            // The failing shape: an outer "Schedules"/"Schedule" group wraps the Rows group, so rows
            // render as Table > Schedules > Schedule > Rows > Row and the flat reader finds no rows.
            var xml =
                "<Query xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<RootNodeName>Table</RootNodeName>"
                + "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups>"
                + "<Group GroupName=\"Headers\"><Output xsi:type=\"RenderValue\" Name=\"col\" Value=\"Code\" /></Group>"
                + "<Group GroupName=\"Schedules\" ItemName=\"Schedule\" Selector=\"/Employer/[EmployerKey]/PaySchedules\" UniqueKeyVariable=\"[ScheduleKey]\">"
                + "<Group GroupName=\"Rows\" ItemName=\"Row\" Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Output xsi:type=\"RenderValue\" Name=\"col\" Value=\"[ScheduleKey]\" />"
                + "</Group>"
                + "</Group>"
                + "</Groups></Query>";

            var codes = this.linter.Lint(xml).Select(d => d.Code).ToList();

            Assert.That(codes, Has.Member("TabularRowsNested"));
        }

        [Test]
        public void Lint_TableQuery_NoRowsGroup_WarnsTabularMissingRowsGroup()
        {
            var xml =
                "<Query xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<RootNodeName>Table</RootNodeName>"
                + "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups>"
                + "<Group GroupName=\"Headers\"><Output xsi:type=\"RenderValue\" Name=\"col\" Value=\"Code\" /></Group>"
                + "<Group Selector=\"/Employer/[EmployerKey]/Employees\">"
                + "<Output xsi:type=\"RenderProperty\" Name=\"col\" Property=\"Code\" />"
                + "</Group>"
                + "</Groups></Query>";

            var codes = this.linter.Lint(xml).Select(d => d.Code).ToList();

            Assert.That(codes, Has.Member("TabularMissingRowsGroup"));
        }

        [Test]
        public void Lint_NonTableQuery_NoRowsPlacementWarning()
        {
            var xml = Query(
                "<Variables><Variable Name=\"[EmployerKey]\" Value=\"ER001\" /></Variables>"
                + "<Groups><Group Selector=\"/Employer/[EmployerKey]/Employees\"><Output xsi:type=\"RenderEntity\" /></Group></Groups>");

            var codes = this.linter.Lint(xml).Select(d => d.Code).ToList();

            Assert.That(codes, Has.No.Member("TabularMissingRowsGroup"));
            Assert.That(codes, Has.No.Member("TabularRowsNested"));
        }

        [Test]
        public void Lint_ReportBuilderExampleQuery_FlagsAllKnownDefects()
        {
            // The real defective query produced by the Report Builder assistant: an invalid nested
            // route, an Order in an entity-less trailing group, an unassigned [RowKey] variable and
            // a per-entity render over the PayRuns collection.
            const string Xml = """
                <Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <RootNodeName>Table</RootNodeName>
                  <Variables>
                    <Variable Name="[EmployerKey]" Value="CP002" />
                  </Variables>
                  <Groups>
                    <Group GroupName="Headers">
                      <Output xsi:type="RenderValue" Name="col" Value="Code" />
                      <Output xsi:type="RenderValue" Name="col" Value="MostRecentPaymentDate" />
                    </Group>
                    <Group GroupName="Rows" ItemName="Row" Selector="/Employer/[EmployerKey]/Employees" Optimise="true">
                      <Output xsi:type="RenderProperty" Name="col" Property="Code" />
                      <Group Selector="/Employer/[EmployerKey]/Employees/[RowKey]/PaySchedule">
                        <Output xsi:type="RenderProperty" Name="col" Property="PaySchedule" />
                      </Group>
                      <Group GroupName="RecentPayments" Selector="/Employer/[EmployerKey]/Employee/[RowKey]/PayRuns" Optimise="true">
                        <Output xsi:type="RenderProperty" Name="col" Property="PaymentDate" Format="yyyy-MM-dd" />
                        <Order xsi:type="Descending" Property="PaymentDate" />
                      </Group>
                      <Group>
                        <Order xsi:type="Ascending" Property="LastName" />
                      </Group>
                    </Group>
                  </Groups>
                </Query>
                """;

            var codes = this.linter.Lint(Xml).Select(d => d.Code).ToList();

            Assert.That(codes, Has.Member("UnknownRoute"), "invalid PaySchedule route");
            Assert.That(codes, Has.Member("OrderInEntityLessGroup"), "order in trailing empty group");
            Assert.That(codes, Has.Member("UnassignedVariable"), "[RowKey] never assigned");
            Assert.That(codes, Has.Member("CollectionRenderInTableRow"), "per-entity render over PayRuns collection");
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
