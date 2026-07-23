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

## Tabular employee listing

- **Request:** A flat table of employees for an employer: code, first name, last name and
  start date, one row per employee.
- **Tags:** tabular, employee, headers-rows, render-property

The tabular pattern applies just as well to a simple property listing as it does to a
gross-to-net report (compare "Tabular gross-to-net report") — a static `Headers` group
followed by a `Rows`/`Row` group with one `Output` per column, matched in order.

```xml
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
```

**Notes:** No aggregation is needed here, so unlike the gross-to-net tabular example there is
no separate "capture into variables, then render in a final group" split — each row's columns
are `RenderProperty` outputs reading straight off the in-scope employee, all sharing
`Name="col"` the same way the `Headers` group does. `Optimise` is safe on the `Rows` group
since only the four listed properties (plus `LastName` for ordering) are read from the
employee entity.

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
      <Output xsi:type="RenderValue" Name="TaxMonth" Value="[TaxPeriod]" />
      <Output xsi:type="RenderValue" Name="StartDate" Value="[TaxPeriodStart]" Format="yyyy-MM-dd" />
      <Output xsi:type="RenderValue" Name="EndDate" Value="[TaxPeriodEnd]" Format="yyyy-MM-dd" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `AllTaxMonths` requires the `[TaxYear]` variable and sets `[TaxPeriod]`,
`[TaxPeriodStart]`, `[TaxPeriodEnd]` — it does **not** set `[LoopVariable]`. Other loop forms:
`AllPaySchedulePeriods`, `CSV:a,b,c` and `Range:1-52`, which expose the current value as
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

## Resolve pay run by payment date

- **Request:** Given a pay schedule unique key and a payment date, resolve the matching pay
  run instance and return its unique key so it can be used in further queries.
- **Tags:** pay-run, pay-schedule, payment-date, direct-selector, render-entity, unique-key, unique-key-variable

The API exposes a dedicated route that resolves a pay run directly by date, so this does not
need a `Filter` over the `PayRuns` collection. Substitute the payment date straight into the
selector path in place of the pay run's unique key. `UniqueKeyVariable` on the group captures
the resolved pay run's own unique key — confirmed to work on single-entity selectors as well
as collection selectors, so it is not limited to the iteration pattern shown elsewhere in this
bank.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>PayRunForPaymentDate</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[PayScheduleKey]" Value="SCH001" />
    <Variable Name="[PaymentDate]" Value="2025-05-31" />
  </Variables>
  <Groups>
    <Group GroupName="PayRun" ItemName="PayRun" Selector="/Employer/[EmployerKey]/PaySchedule/[PayScheduleKey]/PayRun/[PaymentDate]" UniqueKeyVariable="[PayRunKey]">
      <Output xsi:type="RenderValue" Output="Attribute" Name="PayRunKey" Value="[PayRunKey]" />
      <Output xsi:type="RenderEntity" />
    </Group>
  </Groups>
</Query>
```

**Notes:** The selector `/Employer/{employerId}/PaySchedule/{payScheduleId}/PayRun/{effectiveDate:yyyy-MM-dd}`
is a single-entity endpoint keyed by date rather than by unique key — pass the date literally
(`2025-05-31`), not a wildcard or a `UniqueKeyVariable` on the selector path itself. This is
faster and simpler than iterating `/PayRuns` with an `EqualTo` filter on `PaymentDate` when you
already know the date. Because it targets a single entity, `Optimise` does not apply here
(optimisation is collection-only); use `RenderEntity` or specific `RenderProperty` outputs as
needed. The `PayRunKey` attribute captured here can be substituted into subsequent queries,
e.g. `/Employer/[EmployerKey]/PaySchedule/[PayScheduleKey]/PayRun/[PayRunKey]/ReportLines`.

## Using summary report lines

- **Request:** Use the pre-summed pay run report lines to get gross pay, net pay and other
  totals without loading every employee's pay lines.
- **Tags:** report-lines, pay-run-summary, of-type, performance

A pay run's `ReportLines` endpoint returns several report line types (per-employee summaries,
tax summaries, pension summaries, the overall pay run summary, etc.) from one call. Use an
`OfType` filter to select just the pay-run-level summary, which already carries gross, tax,
NI, pension and net totals — no need to sum individual pay lines.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>PayRunSummary</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[PayScheduleKey]" Value="SCH001" />
    <Variable Name="[PayRunKey]" Value="PR001" />
  </Variables>
  <Groups>
    <Group GroupName="Summary" ItemName="ReportLine" Selector="/Employer/[EmployerKey]/PaySchedule/[PayScheduleKey]/PayRun/[PayRunKey]/ReportLines">
      <Filter xsi:type="OfType" Value="ReportLinePayRunSummary" />
      <Output xsi:type="RenderProperty" Name="EmployeeCount" Property="EmployeeCount" />
      <Output xsi:type="RenderProperty" Name="GrossPay" Property="GrossPay" />
      <Output xsi:type="RenderProperty" Name="Tax" Property="Tax" />
      <Output xsi:type="RenderProperty" Name="EmployeeNI" Property="EmployeeNI" />
      <Output xsi:type="RenderProperty" Name="EmployerNI" Property="EmployerNI" />
      <Output xsi:type="RenderProperty" Name="EmployeePension" Property="EmployeePension" />
      <Output xsi:type="RenderProperty" Name="EmployerPension" Property="EmployerPension" />
      <Output xsi:type="RenderProperty" Name="NetPay" Property="NetPay" />
      <Output xsi:type="RenderProperty" Name="EmployerCost" Property="EmployerCost" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `ReportLines` is a plural-type endpoint — always add an `OfType` filter naming the
report line type you want (`ReportLinePayRunSummary`, `ReportLineEmployeeSummary`,
`ReportLineTaxSummary`, `ReportLinePension`, etc.), otherwise every type is returned mixed
together. `ReportLinePayRunSummary` is generated once per pay run calculation, so this is
dramatically cheaper on large employers than summing `PayLines` per employee for the same
totals — reach for it whenever the requirement is an aggregate figure rather than
employee-level detail.

## Using employee summary report lines for a gross-to-net report

- **Request:** For each employee in a pay run, show their name, gross pay, tax, NI, pension
  and net pay — without summing individual pay lines.
- **Tags:** report-lines, employee-summary, of-type, gross-to-net, performance

`ReportLineEmployeeSummary` is generated once per employee per pay run calculation and already
carries the gross-to-net breakdown, so it replaces the pattern of iterating employees and
summing `PayLines` (compare the "Tabular gross-to-net report" example) with a single flat
collection fetch and an `OfType` filter.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>EmployeeSummaries</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[PayScheduleKey]" Value="SCH001" />
    <Variable Name="[PayRunKey]" Value="PR001" />
  </Variables>
  <Groups>
    <Group GroupName="Employees" ItemName="ReportLine" Selector="/Employer/[EmployerKey]/PaySchedule/[PayScheduleKey]/PayRun/[PayRunKey]/ReportLines">
      <Filter xsi:type="OfType" Value="ReportLineEmployeeSummary" />
      <Output xsi:type="RenderProperty" Name="EmployeeCode" Property="EmployeeCode" />
      <Output xsi:type="RenderProperty" Name="FirstName" Property="FirstName" />
      <Output xsi:type="RenderProperty" Name="LastName" Property="LastName" />
      <Output xsi:type="RenderProperty" Name="GrossPay" Property="GrossPay" />
      <Output xsi:type="RenderProperty" Name="Tax" Property="Tax" />
      <Output xsi:type="RenderProperty" Name="EmployeeNI" Property="EmployeeNI" />
      <Output xsi:type="RenderProperty" Name="EmployeePension" Property="EmployeePension" />
      <Output xsi:type="RenderProperty" Name="NetPay" Property="NetPay" />
      <Order xsi:type="Ascending" Property="LastName" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `ReportLineEmployeeSummary` carries `EmployeeKey` too, so it can drive further
per-employee lookups (e.g. `/Employer/[EmployerKey]/Employee/[EmployeeKey]/...`) without an
extra `Employees` group. It also has `EmployerNI` and `EmployerPension` for employer-side
costs, and `Description`/`Value` fields inherited from the base `ReportLine` shape. As with
the pay-run summary, always keep the `OfType="ReportLineEmployeeSummary"` filter — dropping it
returns every report line type from the pay run mixed together in one collection.

## Iterating employees with an optimised query

- **Request:** List employees for an employer showing only code, first name and last name,
  without loading the full employee object graph (address, bank account, base pay, cost
  splits, etc).
- **Tags:** employee, optimise, performance, render-property

Add `Optimise="true"` to the entity group. The query engine then fetches only the properties
referenced in `Output`, `Filter` and `Order` elements for that group, skipping nested child
entities entirely.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>EmployeeList</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
  </Variables>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees" Optimise="true">
      <Output xsi:type="RenderProperty" Name="Code" Property="Code" />
      <Output xsi:type="RenderProperty" Name="FirstName" Property="FirstName" />
      <Output xsi:type="RenderProperty" Name="LastName" Property="LastName" />
      <Order xsi:type="Ascending" Property="LastName" />
    </Group>
  </Groups>
</Query>
```

**Notes:** `Optimise` only applies to groups selecting a *collection* endpoint (e.g.
`/Employer/[EmployerKey]/Employees`), not a single-entity lookup, and it is incompatible with
`RenderEntity` — list only the specific `RenderProperty` outputs you need. On a
plural-type/multi-type endpoint, an optimised group can only see properties defined on the
common base type of the returned entities. Reach for this whenever a query only needs a
handful of scalar fields from a large employee (or similarly heavy) collection.

## Reporting on pay lines by pay code

- **Request:** For each employee, total the value of pay lines matching a specific pay code
  (e.g. a bonus or allowance) for a given payment date.
- **Tags:** pay-lines, pay-code, bonus, filters, sum, variables

`PayCode` is a loose linkage on the base `PayLine` type identifying the kind of payment or
deduction (independent of the concrete pay line entity type). Filter on it directly rather
than using `OfType`, which matches the .NET entity type instead.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>PayCodeReport</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[PaymentDate]" Value="2025-05-31" />
    <Variable Name="[PayCode]" Value="BONUS" />
  </Variables>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees" UniqueKeyVariable="[EmployeeKey]" Optimise="true">
      <Output xsi:type="RenderValue" Output="Variable" Name="[PayCodeValue]" Value="0" />
      <Output xsi:type="RenderProperty" Output="Variable" Name="[FirstName]" Property="FirstName" />
      <Output xsi:type="RenderProperty" Output="Variable" Name="[LastName]" Property="LastName" />
      <Group Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines" Predicate="PaymentDate = [PaymentDate]">
        <Filter xsi:type="EqualTo" Property="PayCode" Value="[PayCode]" />
        <Output xsi:type="Sum" Output="Variable" Name="[PayCodeValue]" Property="Value" />
      </Group>
      <Group>
        <Output xsi:type="RenderValue" Name="FullName" Value="[FirstName] [LastName]" />
        <Output xsi:type="RenderValue" Name="Value" Value="[PayCodeValue]" Format="0.00" />
      </Group>
    </Group>
  </Groups>
</Query>
```

**Notes:** `[PayCodeValue]` is reset to `0` at the top of each employee iteration, same
reasoning as the net pay example — otherwise an employee with no matching pay lines would
repeat the previous employee's total. The employee group uses `Optimise="true"` since only
name fields are needed from the employee entity itself; the nested `PayLines` group is
unaffected by that setting. Swap the `EqualTo` filter for `WithinArray` to sum several pay
codes at once, e.g. `Value="BONUS,COMMISSION"`.

## Holiday scheme accrual per employee

- **Request:** A table of holiday accrual for all employees in a tax year: scheme name,
  employee, annual entitlement, units accrued, units reclaimed and remaining balance.
- **Tags:** tabular, holiday-scheme, holiday-accrual, pay-instructions, link-selector, active-within, take-first, sum, expression-calculator, render-tax-period-date

Holiday data spans three related entities, and no single endpoint joins them:

1. An employee is linked to a scheme by a `HolidaySchemePayInstruction` in their
   `PayInstructions` collection. Per-employee overrides (`AnnualEntitlementDays`,
   `AccrualType`, join/exit dates) live here, as does `HolidayScheme` — a `Link` to the
   scheme entity.
2. The `HolidayScheme` entity carries the scheme-level settings (`SchemeName`,
   `AnnualEntitlementWeeks`, carry-over rules).
3. Accrual and usage are recorded as `PayLineHoliday` pay lines: `UnitsAccrued` and
   `UnitsDepleted` per pay run. The balance is derived, not stored — sum both and subtract.

The query walks that chain per employee: find the active holiday scheme pay instruction,
follow its link to the scheme, sum the holiday pay lines, then render the row.

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>Table</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[TaxYear]" Value="2025" />
  </Variables>
  <Groups>
    <Group GroupName="Headers">
      <Output xsi:type="RenderValue" Name="col" Value="HolidayScheme" />
      <Output xsi:type="RenderValue" Name="col" Value="EmployeeCode" />
      <Output xsi:type="RenderValue" Name="col" Value="FirstName" />
      <Output xsi:type="RenderValue" Name="col" Value="LastName" />
      <Output xsi:type="RenderValue" Name="col" Value="AnnualEntitlement" />
      <Output xsi:type="RenderValue" Name="col" Value="UnitOfMeasure" />
      <Output xsi:type="RenderValue" Name="col" Value="Accrued" />
      <Output xsi:type="RenderValue" Name="col" Value="Reclaimed" />
      <Output xsi:type="RenderValue" Name="col" Value="Balance" />
      <Output xsi:type="RenderTaxPeriodDate" Output="Variable" DisplayName="[TaxYearStart]" TaxYear="[TaxYear]" TaxPeriod="1" PayFrequency="Monthly" Format="yyyy-MM-dd" />
      <Output xsi:type="RenderTaxPeriodDate" Output="Variable" DisplayName="[TaxYearEnd]" TaxYear="[TaxYear]" TaxPeriod="12" PayFrequency="Monthly" Format="yyyy-MM-dd" EndDate="true" />
    </Group>
    <Group GroupName="Rows" ItemName="Row" Selector="/Employer/[EmployerKey]/Employees" Optimise="true" UniqueKeyVariable="[EmployeeKey]">
      <Output xsi:type="RenderValue" Output="Variable" Name="[HolidayScheme]" Value="" />
      <Output xsi:type="RenderProperty" Output="Variable" Name="[EmployeeCode]" Property="Code" />
      <Output xsi:type="RenderProperty" Output="Variable" Name="[FirstName]" Property="FirstName" />
      <Output xsi:type="RenderProperty" Output="Variable" Name="[LastName]" Property="LastName" />
      <Output xsi:type="RenderValue" Output="Variable" Name="[AnnualEntitlement]" Value="" />
      <Output xsi:type="RenderValue" Output="Variable" Name="[UnitOfMeasure]" Value="" />
      <Output xsi:type="RenderValue" Output="Variable" Name="[Accrued]" Value="0.00" />
      <Output xsi:type="RenderValue" Output="Variable" Name="[Reclaimed]" Value="0.00" />
      <Output xsi:type="RenderValue" Output="Variable" Name="[HolidaySchemeLink]" Value="" />
      <Group Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayInstructions" Predicate="OFTYPE = 'HolidaySchemePayInstruction'">
        <Filter xsi:type="ActiveWithin" Value="[TaxYearStart],[TaxYearEnd]" />
        <Filter xsi:type="TakeFirst" Value="1" />
        <Output xsi:type="RenderProperty" Output="Variable" Name="[HolidaySchemeLink]" Property="HolidayScheme.Href" />
        <Output xsi:type="RenderProperty" Output="Variable" Name="[UnitOfMeasure]" Property="AccrualType" />
        <Order xsi:type="Descending" Property="StartDate" />
      </Group>
      <Group Selector="[HolidaySchemeLink]">
        <Condition xsi:type="WhenNot" ValueA="[HolidaySchemeLink]" ValueB="" />
        <Output xsi:type="RenderProperty" Output="Variable" Name="[HolidayScheme]" Property="SchemeName" />
        <Output xsi:type="RenderProperty" Output="Variable" Name="[AnnualEntitlement]" Property="AnnualEntitlementWeeks" />
      </Group>
      <Group Selector="/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines" Predicate="OFTYPE = 'PayLineHoliday' AND PaymentDate &gt;= '[TaxYearStart]' AND PaymentDate &lt;= '[TaxYearEnd]'">
        <Output xsi:type="Sum" Output="Variable" Name="[Accrued]" Property="UnitsAccrued" />
        <Output xsi:type="Sum" Output="Variable" Name="[Reclaimed]" Property="UnitsDepleted" />
      </Group>
      <Group>
        <Output xsi:type="RenderValue" Name="col" Value="[HolidayScheme]" />
        <Output xsi:type="RenderValue" Name="col" Value="[EmployeeCode]" />
        <Output xsi:type="RenderValue" Name="col" Value="[FirstName]" />
        <Output xsi:type="RenderValue" Name="col" Value="[LastName]" />
        <Output xsi:type="RenderValue" Name="col" Value="[AnnualEntitlement]" Format="0.00" />
        <Output xsi:type="RenderValue" Name="col" Value="[UnitOfMeasure]" />
        <Output xsi:type="RenderValue" Name="col" Value="[Accrued]" />
        <Output xsi:type="RenderValue" Name="col" Value="[Reclaimed]" />
      </Group>
      <Group>
        <Output xsi:type="ExpressionCalculator" Name="col" Format="0.00" Expression="[Accrued] - [Reclaimed]" />
      </Group>
    </Group>
  </Groups>
</Query>
```

**Notes:** Several techniques combine here:

- **Tax year boundaries as variables:** the two `RenderTaxPeriodDate` outputs in the
  `Headers` group use `Output="Variable"` to derive the tax year start/end dates from
  `[TaxYear]`. Variable outputs render nothing, so they add no columns; for date renders the
  target variable is named in `DisplayName`, not `Name`.
- **Current instruction selection:** `ActiveWithin` keeps only pay instructions active in
  the date range, then `Order Descending` on `StartDate` plus `TakeFirst 1` picks the most
  recent — the standard "current instruction" idiom (also required to keep table columns
  stable, since `PayInstructions` is a collection).
- **Link following:** `HolidayScheme.Href` (a dotted path into the instruction's `Link`
  property) is captured into `[HolidaySchemeLink]`, and the next group uses that variable
  *as its selector* to load the scheme entity itself. The `WhenNot` condition skips the
  lookup for employees with no holiday scheme, whose scheme columns render blank.
- **Predicate OFTYPE:** both nested fetch groups narrow by entity type in the `Predicate`
  (`OFTYPE = '...'`) so the type filter and date range are applied at the database rather
  than after fetch.
- Accrual variables are reset per row, and the final `ExpressionCalculator` group derives
  the `Balance` column as `[Accrued] - [Reclaimed]`.

## Employees carrying a named meta data item

- **Request:** List employees that have a "CostCentre" meta data item, showing its value.
- **Tags:** meta-data, employee, contain-filter, dynamic-properties

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>EmployeesWithCostCentre</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
  </Variables>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees" Predicate="MetaData.CostCentre != null">
      <Output xsi:type="RenderProperty" Name="Code" Property="Code" />
      <Output xsi:type="RenderProperty" Name="Surname" Property="LastName" />
      <Output xsi:type="RenderProperty" Name="CostCentre" Property="MetaData.CostCentre" />
    </Group>
  </Groups>
</Query>
```

**Notes:** Meta data items are addressed with a **pseudo property** dot notation —
`MetaData.CostCentre` reads the item named "CostCentre". The item name is data, not a schema
member, so `get_schema` will never list it. Never navigate the underlying collection:
`MetaData.Items CONTAINS 'CostCentre'` is **invalid RQL**. A missing item resolves to `null`, so
`MetaData.CostCentre != null` is the idiomatic existence test and, being a concrete item, it is
legal in a group `Predicate`. To test existence without knowing the name at authoring time, use a
`Contain` filter over `MetaData.AllItemNames` instead — but only in a `Filter`, never a
`Predicate`.

## Dynamically report all meta data items per employee

- **Request:** For every employee, list each meta data item name and value without knowing the names in advance.
- **Tags:** meta-data, all-item-names, loop-expressions, csv-loop, dynamic-properties

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>EmployeeMetaData</RootNodeName>
  <Variables>
    <Variable Name="[EmployerKey]" Value="ER001" />
    <Variable Name="[ItemNames]" Value="" />
  </Variables>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/[EmployerKey]/Employees">
      <Output xsi:type="RenderProperty" Name="Code" Property="Code" />
      <Output xsi:type="RenderProperty" Output="Variable" Name="[ItemNames]" Property="MetaData.AllItemNames" />
      <Group GroupName="MetaDataItems" ItemName="Item" LoopExpression="CSV:[ItemNames]">
        <Output xsi:type="RenderValue" Name="ItemName" Value="[LoopVariable]" />
        <Output xsi:type="RenderProperty" Name="ItemValue" Property="MetaData.[LoopVariable]" />
      </Group>
    </Group>
  </Groups>
</Query>
```

**Notes:** `MetaData.AllItemNames` is an RQL specific extension returning a comma separated list of
every meta data item name on the entity (e.g. `NameA,NameB,NameC`). Captured into a variable it
feeds a `CSV:` loop expression directly, giving one iteration per item with the name exposed as
`[LoopVariable]`. The value is then read back with `MetaData.[LoopVariable]` — the variable is
substituted into the pseudo property path before resolution, which is what makes name-agnostic
meta data reporting possible. `AllItemNames` is **not valid in a group `Predicate`** because it has
no direct SQL translation; keep it in `Output`, `Filter` and `Condition` positions.
