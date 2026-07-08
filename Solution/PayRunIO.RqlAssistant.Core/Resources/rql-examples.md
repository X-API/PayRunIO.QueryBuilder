# RQL Example Bank

Curated, validated example queries for common PayRunIO reporting requests. Each example
states the natural-language request it answers, the tags describing the constructs it uses,
and the complete `<Query>` XML. Adapt the closest example rather than composing RQL from
scratch: replace variable values, selectors and property names to fit the request.

Structural rules every example follows (enforced by QuerySchema.xsd):

- Group children must appear in this order: `<Condition>`, `<Filter>`, `<Output>`, `<Order>`, `<Group>`.
- All outputs of a group come **before** its sub-groups; use a trailing sub-group to render
  values after nested aggregation (the "final rendering group" idiom).
- Initialise any variable written by `Sum`/`VariableSum` at the start of each iteration to
  stop values leaking between rows.

## All employers

- **Request:** List all employers in the application scope.
- **Tags:** employer, render-entity, basics

The minimal RQL shape: one group, one `RenderEntity` output.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>Employers</RootNodeName>
  <Groups>
    <Group GroupName="Employers" ItemName="Employer" Selector="/Employers">
      <Output xsi:type="RenderEntity" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `RenderEntity` emits the whole matched entity. Use `RenderProperty` outputs
instead when only specific fields are wanted.

## Employee list with selected properties

- **Request:** List employees for an employer showing code, first name and last name, ordered by surname.
- **Tags:** employee, render-property, ordering

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>EmployeeList</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
  </Variables>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees">
      <Output xsi:type="RenderProperty" Name="Code" Property="Code" />
      <Output xsi:type="RenderProperty" Name="FirstName" Property="FirstName" />
      <Output xsi:type="RenderProperty" Name="LastName" Property="LastName" />
      <Order xsi:type="Ascending" Property="LastName" />
      <Order xsi:type="Ascending" Property="FirstName" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `<Order>` elements come after the `<Output>` elements (XSD sequence order).
Multiple orders apply in the order declared.

## List all employees including API resource key (AKA Unique Key)

- **Request:** List all employees for an employer, including each employee's API resource key (unique key) as an attribute.
- **Tags:** employee, unique-key-variable, render-value, attribute

Utilises the Entity Group unique key variable specification to capture the scoped entity unique key and then writes to an attribute output.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>EmployeeList</RootNodeName>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/ER001/Employees" UniqueKeyVariable="[EmployeeKey]">
      <Output xsi:type="RenderValue" Output="Attribute" Name="EmployeeKey" Value="[EmployeeKey]" />
      <Output xsi:type="RenderEntity" />
    </Group>
  </Groups>
</Query>
```

**Notes:** The captured API resource key (defined by ```UniqueKeyVariable```) can be used in sub groups to generate addtional hierachical selector values.
For example: ```/Employer/ER001/Employee/[EmployeeKey]/PayLines```

## Current employees excluding leavers

- **Request:** List employees who have not left (no leaving date set).
- **Tags:** employee, filters, is-null

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>CurrentEmployees</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
  </Variables>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees">
      <Filter xsi:type="IsNull" Property="LeavingDate" />
      <Output xsi:type="RenderProperty" Name="Code" Property="Code" />
      <Output xsi:type="RenderProperty" Name="FirstName" Property="FirstName" />
      <Output xsi:type="RenderProperty" Name="LastName" Property="LastName" />
    </Group>
  </Groups>
</Query>
```

**Notes:** Invert with `IsNotNull` to list only leavers.

## Leavers within a date range

- **Request:** List employees who left between two dates, with their leaving date.
- **Tags:** employee, filters, between, date-range

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>Leavers</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[FromDate]" Value="2025-04-06" />
    <Variable Name="[ToDate]" Value="2026-04-05" />
  </Variables>
  <Groups>
    <Group GroupName="Leavers" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees">
      <Filter xsi:type="IsNotNull" Property="LeavingDate" />
      <Filter xsi:type="Between" Property="LeavingDate" Value="[FromDate]" Value2="[ToDate]" />
      <Output xsi:type="RenderProperty" Name="Code" Property="Code" />
      <Output xsi:type="RenderProperty" Name="LastName" Property="LastName" />
      <Output xsi:type="RenderProperty" Name="LeavingDate" Property="LeavingDate" Format="yyyy-MM-dd" />
      <Order xsi:type="Ascending" Property="LeavingDate" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `Between` is inclusive of both bounds. Filters are ANDed unless marked `IsOr="true"`.
For joiners in a range, filter on `StartDate` instead.

## Ten most recent joiners

- **Request:** Show the 10 employees with the most recent start dates.
- **Tags:** employee, take-first, ordering, top-n

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>RecentJoiners</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
  </Variables>
  <Groups>
    <Group GroupName="Joiners" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees">
      <Filter xsi:type="TakeFirst" Value="10" />
      <Output xsi:type="RenderProperty" Name="Code" Property="Code" />
      <Output xsi:type="RenderProperty" Name="LastName" Property="LastName" />
      <Output xsi:type="RenderProperty" Name="StartDate" Property="StartDate" Format="yyyy-MM-dd" />
      <Order xsi:type="Descending" Property="StartDate" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `TakeFirst` restricts the matched set and should always be paired with an
`<Order>` so "first" is well defined.

## Employees grouped under each employer

- **Request:** For every employer, list its employees (two-level hierarchy).
- **Tags:** employer, employee, nested-groups, unique-key-variable

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>EmployerEmployees</RootNodeName>
  <Groups>
    <Group GroupName="Employers" ItemName="Employer" Selector="/Employers" UniqueKeyVariable="[EmployerKey]">
      <Output xsi:type="RenderProperty" Name="EmployerName" Property="Name" />
      <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees">
        <Output xsi:type="RenderProperty" Name="Code" Property="Code" />
        <Output xsi:type="RenderProperty" Name="LastName" Property="LastName" />
      </Group>
    </Group>
  </Groups>
</Query>
```

**Notes:** `UniqueKeyVariable` captures each matched employer's key so the nested selector
can substitute it. This is the standard pattern for walking the API hierarchy.

## Pay runs for a payment date

- **Request:** Find the pay runs on a schedule with a specific payment date.
- **Tags:** pay-run, pay-schedule, filters, equal-to

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>PayRuns</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[PayScheduleKey]" Value="SCH001" />
    <Variable Name="[PaymentDate]" Value="2025-05-31" />
  </Variables>
  <Groups>
    <Group GroupName="PayRuns" ItemName="PayRun" Selector="/Employer/[EmployerKey]/PaySchedule/[PayScheduleKey]/PayRuns">
      <Filter xsi:type="EqualTo" Property="PaymentDate" Value="[PaymentDate]" />
      <Output xsi:type="RenderEntity" />
    </Group>
  </Groups>
</Query>
```

**Notes:** The pay run entity carries `PaymentDate`, `PeriodStart`, `PeriodEnd`, `TaxYear`
and `TaxPeriod` — filter on any of them the same way.

## Net pay per employee for a payment date

- **Request:** For each employee, show their name and total net pay for a given payment date, plus a grand total.
- **Tags:** employee, pay-lines, sum, variables, variable-sum, predicate, totals

Net pay is the sum of all pay line values (deductions are stored as negatives).
Note the `[NetPay]` reset at the top of each employee iteration — without it an employee
with no pay lines would repeat the previous employee's total.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>NetPayReport</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[PaymentDate]" Value="2025-05-31" />
    <Variable Name="[TotalNetPay]" Value="0" />
  </Variables>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees" UniqueKeyVariable="[EmployeeKey]">
      <Output xsi:type="RenderValue" Output="Variable" Name="[NetPay]" Value="0" />
      <Output xsi:type="RenderProperty" Output="Variable" Name="[FirstName]" Property="FirstName" />
      <Output xsi:type="RenderProperty" Output="Variable" Name="[LastName]" Property="LastName" />
      <Group Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines" Predicate="PaymentDate = [PaymentDate]">
        <Output xsi:type="Sum" Output="Variable" Name="[NetPay]" Property="Value" />
      </Group>
      <Group>
        <Output xsi:type="RenderValue" Name="FullName" Value="[FirstName] [LastName]" />
        <Output xsi:type="RenderValue" Name="NetPay" Value="[NetPay]" Format="0.00" />
        <Output xsi:type="RenderValue" Output="Attribute" Name="Key" Value="[EmployeeKey]" />
        <Output xsi:type="RenderValue" Output="VariableSum" Name="[TotalNetPay]" Value="[NetPay]" />
      </Group>
    </Group>
    <Group GroupName="Totals">
      <Output xsi:type="RenderValue" Name="NetPay" Value="[TotalNetPay]" Format="0.00" />
    </Group>
  </Groups>
</Query>
```

**Notes:** The trailing anonymous `<Group>` renders values *after* the nested aggregation
group has run — outputs placed directly on the employee group would execute before it.
`Predicate` narrows the database fetch; a `<Filter xsi:type="EqualTo">` after fetch is
equivalent but slower on large sets.

## PAYE tax deducted for an employee

- **Request:** Total PAYE tax deducted for one employee on a payment date.
- **Tags:** pay-lines, of-type, tax, sum, negate

Deduction pay lines are negative; `Negate="true"` flips the sign so tax reads as a
positive amount.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>ResultSet</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[EmployeeKey]" Value="EE001" />
    <Variable Name="[PaymentDate]" Value="2025-05-31" />
  </Variables>
  <Groups>
    <Group GroupName="SumOfTax" Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines">
      <Filter xsi:type="OfType" Value="PayLineTax" />
      <Filter xsi:type="EqualTo" Property="PaymentDate" Value="[PaymentDate]" />
      <Output xsi:type="Sum" Name="Tax" Property="Value" Negate="true" />
    </Group>
  </Groups>
</Query>
```

**Notes:** Pay line collections mix entity types; `OfType` selects one type. Other common
types: `PayLineNi`, `PayLinePension`, `PayLineStudentLoan`, `PayLineSalary`.

## National insurance contributions for an employee

- **Request:** Employee and employer NI contribution totals for a payment date.
- **Tags:** pay-lines, of-type, ni, sum, negate

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>ResultSet</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[EmployeeKey]" Value="EE001" />
    <Variable Name="[PaymentDate]" Value="2025-05-31" />
  </Variables>
  <Groups>
    <Group GroupName="NiContributions" Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines">
      <Filter xsi:type="OfType" Value="PayLineNi" />
      <Filter xsi:type="EqualTo" Property="PaymentDate" Value="[PaymentDate]" />
      <Output xsi:type="Sum" Name="EmployeeNi" Property="Value" Negate="true" />
      <Output xsi:type="Sum" Name="EmployerNi" Property="EmployerNI" Negate="true" />
    </Group>
  </Groups>
</Query>
```

**Notes:** Employee NI is the `Value` property; employer NI is the separate `EmployerNI`
property on the same `PayLineNi` entity.

## Pension contributions for an employee

- **Request:** Employee and employer pension contribution totals for a payment date.
- **Tags:** pay-lines, of-type, pension, sum, negate

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>ResultSet</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[EmployeeKey]" Value="EE001" />
    <Variable Name="[PaymentDate]" Value="2025-05-31" />
  </Variables>
  <Groups>
    <Group GroupName="PensionContributions" Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines">
      <Filter xsi:type="OfType" Value="PayLinePension" />
      <Filter xsi:type="EqualTo" Property="PaymentDate" Value="[PaymentDate]" />
      <Output xsi:type="Sum" Name="EmployeeContribution" Property="Value" Negate="true" />
      <Output xsi:type="Sum" Name="EmployerContribution" Property="EmployerContribution" Negate="true" />
    </Group>
  </Groups>
</Query>
```

**Notes:** Employee contribution is `Value`; employer contribution is
`EmployerContribution` on the same `PayLinePension` entity.

## Tax code and NI letter from the employee summary

- **Request:** Show an employee's current tax code, tax basis and NI letter.
- **Tags:** employee-summary, render-property, tax-code, ni-letter

These values live on the `EmployeeSummary` entity (a descendant of the employee), not on
the employee itself.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>ResultSet</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[EmployeeKey]" Value="EE001" />
  </Variables>
  <Groups>
    <Group GroupName="Employee" ItemName="Summary" Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/Summary">
      <Output xsi:type="RenderProperty" Name="TaxCode" Property="TaxCode" />
      <Output xsi:type="RenderProperty" Name="TaxBasis" Property="TaxBasis" />
      <Output xsi:type="RenderProperty" Name="NiLetter" Property="NiLetter" />
    </Group>
  </Groups>
</Query>
```

**Notes:** Use `/Employer/{id}/Employee/{id}/Summary` for the current summary; append an
effective date to select the summary at a point in time.

## Pay lines for specific tax periods using OR filters

- **Request:** Sum pay line values for tax year 2025, periods 1 or 2, across all employees.
- **Tags:** pay-lines, filters, is-or, wildcard-selector, sum

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>ResultSet</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
  </Variables>
  <Groups>
    <Group GroupName="PayLines" ItemName="Total" Selector="/Employer/[EmployerKey]/Employee/*/PayLines">
      <Filter xsi:type="EqualTo" Property="TaxYear" Value="2025" />
      <Filter xsi:type="EqualTo" Property="TaxPeriod" Value="1" IsOr="true" />
      <Filter xsi:type="EqualTo" Property="TaxPeriod" Value="2" IsOr="true" />
      <Output xsi:type="Sum" Name="TotalNetPay" Property="Value" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `*` in a selector wildcards a key segment (here: every employee). `IsOr="true"`
filters form one OR block; plain filters remain ANDed with it.

## Conditional groups by pay frequency

- **Request:** Render a different section depending on whether the schedule is monthly or weekly.
- **Tags:** conditions, when-equal-to, branching

RQL has no else-branch: use two sibling groups with opposite conditions.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>ResultSet</RootNodeName>
  <Variables>
    <Variable Name="[PayFrequency]" Value="Monthly" />
  </Variables>
  <Groups>
    <Group GroupName="MonthlySection">
      <Condition xsi:type="WhenEqualTo" ValueA="[PayFrequency]" ValueB="Monthly" />
      <Output xsi:type="RenderValue" Name="Frequency" Value="This schedule pays monthly" />
    </Group>
    <Group GroupName="OtherSection">
      <Condition xsi:type="WhenNotEqualTo" ValueA="[PayFrequency]" ValueB="Monthly" />
      <Output xsi:type="RenderValue" Name="Frequency" Value="This schedule does not pay monthly" />
    </Group>
  </Groups>
</Query>
```

**Notes:** Conditions are evaluated before the selector; a failed condition skips the whole
group including nested groups. `<Condition>` elements must be the first children of the group.

## Loop over tax months

- **Request:** For each tax month of a tax year, render the period number with its start and end dates.
- **Tags:** loop-expressions, tax-period, render-tax-period-date

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>TaxMonths</RootNodeName>
  <Variables>
    <Variable Name="[TaxYear]" Value="2025" />
  </Variables>
  <Groups>
    <Group GroupName="Periods" ItemName="Period" LoopExpression="AllTaxMonths">
      <Output xsi:type="RenderValue" Name="TaxMonth" Value="[LoopVariable]" />
      <Output xsi:type="RenderTaxPeriodDate" DisplayName="StartDate" TaxYear="[TaxYear]" TaxPeriod="[LoopVariable]" PayFrequency="Monthly" Format="yyyy-MM-dd" />
      <Output xsi:type="RenderTaxPeriodDate" DisplayName="EndDate" TaxYear="[TaxYear]" TaxPeriod="[LoopVariable]" PayFrequency="Monthly" EndDate="true" Format="yyyy-MM-dd" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `AllTaxMonths` requires the `[TaxYear]` variable. Other loop forms:
`AllPaySchedulePeriods`, `CSV:a,b,c` and `Range:1-52`. The current value is always
`[LoopVariable]`.

## Tabular gross-to-net report

- **Request:** A gross-to-net table for all employees on a payment date: code, name, gross, tax, NI, pension, net.
- **Tags:** tabular, gross-to-net, pay-lines, expression-calculator, variables, headers-rows

The tabular pattern: root node `Table`, a static `Headers` group, then a `Rows`/`Row` group
that resets its aggregation variables, captures properties, aggregates nested pay lines,
derives calculated values, and renders every column in a final rendering group.

```xml
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
        <Output xsi:type="RenderValue" Name="col" Value="[Code]" />
        <Output xsi:type="RenderValue" Name="col" Value="[FirstName] [LastName]" />
        <Output xsi:type="RenderValue" Name="col" Value="[Gross]" Format="0.00" />
        <Output xsi:type="RenderValue" Name="col" Value="[Tax]" Format="0.00" />
        <Output xsi:type="RenderValue" Name="col" Value="[EeNi]" Format="0.00" />
        <Output xsi:type="RenderValue" Name="col" Value="[EePension]" Format="0.00" />
        <Output xsi:type="RenderValue" Name="col" Value="[Net]" Format="0.00" />
      </Group>
    </Group>
  </Groups>
</Query>
```

**Notes:** Gross is derived as net plus the (negated, so positive) deductions; add further
deduction types (`PayLineStudentLoan`, attachment orders, etc.) to the expression when the
employer uses them. Every column rendered in the final group must match the header order.
