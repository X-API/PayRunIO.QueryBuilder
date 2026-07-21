namespace PayRunIO.ReportBuilder.Services
{
    public sealed record ReportTemplate(string Key, string Name, string Description, string QueryXml);

    /// <summary>
    /// Seed catalogue of common tabular reports users can open and customise (directly or via the
    /// AI assistant). Queries follow the tabular output pattern verified in the RQL example bank;
    /// add new templates here to grow the list.
    /// </summary>
    public static class CommonReportCatalog
    {
        public static IReadOnlyList<ReportTemplate> All { get; } = new List<ReportTemplate>
            {
                new(
                    "employee-listing",
                    "Employee listing",
                    "A flat table of employees for an employer: code, first name, last name and start date, one row per employee.",
                    """
                    <Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                      <RootNodeName>Table</RootNodeName>
                      <Variables>
                        <Variable Name="[EmployerKey]" Value="ER001" />
                      </Variables>
                      <Groups>
                        <Group GroupName="Headers">
                          <Output xsi:type="RenderValue" Name="col" Value="Code" />
                          <Output xsi:type="RenderValue" Name="col" Value="FirstName" />
                          <Output xsi:type="RenderValue" Name="col" Value="LastName" />
                          <Output xsi:type="RenderValue" Name="col" Value="StartDate" />
                        </Group>
                        <Group GroupName="Rows" ItemName="Row" Selector="/Employer/[EmployerKey]/Employees" Optimise="true">
                          <Output xsi:type="RenderProperty" Name="col" Property="Code" />
                          <Output xsi:type="RenderProperty" Name="col" Property="FirstName" />
                          <Output xsi:type="RenderProperty" Name="col" Property="LastName" />
                          <Output xsi:type="RenderProperty" Name="col" Property="StartDate" Format="yyyy-MM-dd" />
                          <Order xsi:type="Ascending" Property="LastName" />
                        </Group>
                      </Groups>
                    </Query>
                    """),
                new(
                    "gross-to-net",
                    "Gross to net",
                    "A gross-to-net table for all employees on a payment date: code, name, gross, tax, NI, pension and net pay.",
                    """
                    <Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                      <RootNodeName>Table</RootNodeName>
                      <Variables>
                        <Variable Name="[EmployerKey]" Value="ER001" />
                        <Variable Name="[PaymentDate]" Value="2025-05-31" />
                      </Variables>
                      <Groups>
                        <Group GroupName="Headers">
                          <Output xsi:type="RenderValue" Name="col" Value="Code" />
                          <Output xsi:type="RenderValue" Name="col" Value="Name" />
                          <Output xsi:type="RenderValue" Name="col" Value="Gross" />
                          <Output xsi:type="RenderValue" Name="col" Value="Tax" />
                          <Output xsi:type="RenderValue" Name="col" Value="EmployeeNi" />
                          <Output xsi:type="RenderValue" Name="col" Value="EmployeePension" />
                          <Output xsi:type="RenderValue" Name="col" Value="Net" />
                        </Group>
                        <Group>
                          <Output xsi:type="RenderValue" Output="Variable" Name="[TotalGross]" Value="0" />
                          <Output xsi:type="RenderValue" Output="Variable" Name="[TotalTax]" Value="0" />
                          <Output xsi:type="RenderValue" Output="Variable" Name="[TotalEeNi]" Value="0" />
                          <Output xsi:type="RenderValue" Output="Variable" Name="[TotalEePension]" Value="0" />
                          <Output xsi:type="RenderValue" Output="Variable" Name="[TotalNet]" Value="0" />
                        </Group>
                        <Group GroupName="Rows" ItemName="Row" Selector="/Employer/[EmployerKey]/Employees" UniqueKeyVariable="[EmployeeKey]" Optimise="true">
                          <Output xsi:type="RenderValue" Output="Variable" Name="[Net]" Value="0" />
                          <Output xsi:type="RenderValue" Output="Variable" Name="[Tax]" Value="0" />
                          <Output xsi:type="RenderValue" Output="Variable" Name="[EeNi]" Value="0" />
                          <Output xsi:type="RenderValue" Output="Variable" Name="[EePension]" Value="0" />
                          <Output xsi:type="RenderProperty" Output="Variable" Name="[Code]" Property="Code" />
                          <Output xsi:type="RenderProperty" Output="Variable" Name="[FirstName]" Property="FirstName" />
                          <Output xsi:type="RenderProperty" Output="Variable" Name="[LastName]" Property="LastName" />
                          <Group Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines" Predicate="PaymentDate = [PaymentDate]">
                            <Output xsi:type="Sum" Output="Variable" Name="[Net]" Property="Value" />
                          </Group>
                          <Group Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines" Predicate="PaymentDate = [PaymentDate]">
                            <Filter xsi:type="OfType" Value="PayLineTax" />
                            <Output xsi:type="Sum" Output="Variable" Name="[Tax]" Property="Value" Negate="true" />
                          </Group>
                          <Group Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines" Predicate="PaymentDate = [PaymentDate]">
                            <Filter xsi:type="OfType" Value="PayLineNi" />
                            <Output xsi:type="Sum" Output="Variable" Name="[EeNi]" Property="Value" Negate="true" />
                          </Group>
                          <Group Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines" Predicate="PaymentDate = [PaymentDate]">
                            <Filter xsi:type="OfType" Value="PayLinePension" />
                            <Output xsi:type="Sum" Output="Variable" Name="[EePension]" Property="Value" Negate="true" />
                          </Group>
                          <Group>
                            <Output xsi:type="ExpressionCalculator" Output="Variable" Name="[Gross]" Expression="[Net] + [Tax] + [EeNi] + [EePension]" Format="0.00" />
                          </Group>
                          <Group>
                            <Output xsi:type="RenderValue" Output="VariableSum" Name="[TotalGross]" Value="[Gross]" />
                            <Output xsi:type="RenderValue" Output="VariableSum" Name="[TotalTax]" Value="[Tax]" />
                            <Output xsi:type="RenderValue" Output="VariableSum" Name="[TotalEeNi]" Value="[EeNi]" />
                            <Output xsi:type="RenderValue" Output="VariableSum" Name="[TotalEePension]" Value="[EePension]" />
                            <Output xsi:type="RenderValue" Output="VariableSum" Name="[TotalNet]" Value="[Net]" />
                            <Output xsi:type="RenderValue" Name="col" Value="[Code]" />
                            <Output xsi:type="RenderValue" Name="col" Value="[FirstName] [LastName]" />
                            <Output xsi:type="RenderValue" Name="col" Value="[Gross]" Format="0.00" />
                            <Output xsi:type="RenderValue" Name="col" Value="[Tax]" Format="0.00" />
                            <Output xsi:type="RenderValue" Name="col" Value="[EeNi]" Format="0.00" />
                            <Output xsi:type="RenderValue" Name="col" Value="[EePension]" Format="0.00" />
                            <Output xsi:type="RenderValue" Name="col" Value="[Net]" Format="0.00" />
                          </Group>
                        </Group>
                        <Group GroupName="Footer" ItemName="Row">
                          <Output xsi:type="RenderValue" Name="col" Value="" />
                          <Output xsi:type="RenderValue" Name="col" Value="Total" />
                          <Output xsi:type="RenderValue" Name="col" Value="[TotalGross]" Format="0.00" />
                          <Output xsi:type="RenderValue" Name="col" Value="[TotalTax]" Format="0.00" />
                          <Output xsi:type="RenderValue" Name="col" Value="[TotalEeNi]" Format="0.00" />
                          <Output xsi:type="RenderValue" Name="col" Value="[TotalEePension]" Format="0.00" />
                          <Output xsi:type="RenderValue" Name="col" Value="[TotalNet]" Format="0.00" />
                        </Group>
                      </Groups>
                    </Query>
                    """),
            };

        public static ReportTemplate? FindByKey(string? key) =>
            string.IsNullOrEmpty(key) ? null : All.FirstOrDefault(t => t.Key == key);
    }
}
