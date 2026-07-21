# Abstract Guide to the Tubular Output Pattern in RQL

## Conceptual Overview

This pattern produces a tabular report as an XML or JSON response, structured into:
* Top-level filter variables controlling the data scope.
* Static header row defining column names.
* Data rows, each representing an entity instance with values aggregated and formatted.
* An optional footer row, typically holding column totals, rendered after the data rows.
* Nested subgroups used to gather and compute complex aggregated data per row.
* Variables used extensively to hold intermediate results and control output order.
 

## Core Components of the Pattern
- Fixed Root Node Name
 * Tabular queries must always have a root node name of "Table"
- Filter Variables
 * Declared at the root query level inside <Variables>.
 * Define the parameters used to filter/select relevant data entities.
 * Examples: identifiers (employer ID, schedule ID), date filters (payment date).
 * Used via variable substitution in selectors and predicates.
- Headers Group
 * A static group with no selector.
 * Outputs column names as repeated elements (<Output xsi:type="RenderValue" Name="col" Value="ColumnName" />).
 * Sets the structure of the tabular output before the data rows.
- Contextual Data Groups
 * Groups with selectors to fetch related context entities providing metadata or descriptive info (e.g., employer details, schedule details).
 * Output key properties into variables for reuse in data rows.
 * These groups run once per query (typically single entity matched).
- Rows Group
 * The row group must have a group name of "Rows"
 * The row group must have an item name of "Row"
 * The core repeating group representing each data row (e.g., each employee).
 * Selector points to the collection of entities to iterate over.
 * UniqueKeyVariable stores the unique key of each entity for nested selectors.
 * Often includes Optimise="true" for efficiency.
- Variable Initialization
 * At the start of each row iteration, initialize aggregation variables to default values (e.g., zero).
 * Prevents "value leakage" where previous iterations' values persist unintentionally.
- Entity Property Capture
 * Extract simple properties of the row entity into variables for consistent use and formatting.
- Nested Subgroups for Aggregation
 * Nested groups execute queries filtered by parent entity and other criteria (e.g., date).
 * Use predicates or filters to focus on relevant child data subsets.
 * Aggregate numeric values using outputs like <Sum>.
 * Store aggregated results in variables for later calculations and output.
- Derived Calculations
 * Use an expression calculator output to compute values based on previously aggregated variables.
 * Supports arithmetic operations and formatted output.
- Final Rendering Group
 * A subgroup with no selector, executed per row iteration.
 * Outputs the final values (both variables and static fields) in the desired column order.
 * Uses consistent output names and formatting (e.g., decimal precision).
 * May negate values for display purposes (e.g., tax and deductions).
- Footer Group (optional)
 * A static group named "Footer" with an item name of "Row", placed **after** the Rows group and directly under the root `<Groups>`.
 * Renders a single trailing row of `col` values — commonly report column totals — in the same column order and count as the Headers and data rows.
 * Totals are accumulated across the data rows using running-total variables:
   - Initialise each total variable to `0` in an entity-less group *before* the Rows group (so accumulation starts clean).
   - In the row's final rendering group, add the row's value to each running total with `<Output xsi:type="RenderValue" Output="VariableSum" Name="[TotalX]" Value="[X]" />`.
   - Render the accumulated `[TotalX]` variables as the footer's `col` values.
 * Non-numeric footer cells (e.g. a leading blank and a "Total" label) are rendered as literal `col` values so the footer still matches the column count.
 * The footer is entirely optional — omit the Footer group and the total variables when no summary row is wanted.
 

## Execution Flow Summary
- Initialize filter variables to define query scope.
- Output header row statically.
- Load contextual entities and extract metadata into variables.
- (Optional) Initialise running-total variables to zero before iterating.
- Iterate over main data collection (rows):
 * Reset aggregation variables.
 * Capture entity properties.
 * Query nested child entities with filters/predicates.
 * Aggregate values into variables.
 * Calculate derived metrics.
 * Render all values in order as a single tabular row.
 * (Optional) Accumulate each rendered value into its running total with VariableSum.
- (Optional) Output the Footer group, rendering the running totals as a single trailing row.
 

## Benefits of this Pattern
 * Separation of concerns: headers, context, data rows handled distinctly.
 * Clear variable lifecycle ensures data integrity per row.
 * Nested groups allow complex data aggregation while maintaining modularity.
 * Variable substitution and initialization enable dynamic and safe query execution.
 * Output ordering controlled precisely by the final rendering group.
 * Optimisation support for efficient data retrieval.
 

## Abstract Template Structure (Pseudocode)
```xml 
<Query>
    <!-- Fixed Root Node Name -->
	<RootNodeName>Table</RootNodeName>
	<Variables>
		<Variable Name="[Filter1]" Value="..."/>
		<Variable Name="[Filter2]" Value="..."/>
		<!-- ... -->
	</Variables>
	<Groups>
		<!-- Headers -->
		<Group GroupName="Headers">
			<Output xsi:type="RenderValue" Name="col" Value="Column1"/>
			<Output xsi:type="RenderValue" Name="col" Value="Column2"/>
			<!-- ... -->
		</Group>
		<!-- Contextual Data -->
		<Group Selector="/ContextEntity/[Filter1]">
			<Output xsi:type="RenderProperty" Output="Variable" Name="[ContextVar1]" Property="Property1"/>
			<!-- ... -->
		</Group>
		<!-- Optional: initialise running totals before iterating rows -->
		<Group>
			<Output xsi:type="RenderValue" Output="Variable" Name="[TotalAgg1]" Value="0"/>
			<!-- ... -->
		</Group>
		<!-- Rows -->
		<Group GroupName="Rows" ItemName="Row" Selector="/MainCollection/[Filter1]/[Filter2]" UniqueKeyVariable="[RowKey]" Optimise="true">
			<!-- Initialize aggregation variables -->
			<Output xsi:type="RenderValue" Output="Variable" Name="[AggVar1]" Value="0"/>
			<Output xsi:type="RenderValue" Output="Variable" Name="[AggVar2]" Value="0"/>
			<!-- ... -->
			<!-- Capture properties for row -->
			<Output xsi:type="RenderProperty" Output="Variable" Name="[PropVar1]" Property="Prop1"/>
			<!-- ... -->
			<!-- Nested groups for aggregations -->
			<Group Selector="/MainCollection/[Filter1]/Entity/[RowKey]/ChildCollection" Predicate="FilterCondition">
				<Output xsi:type="Sum" Output="Variable" Name="[AggVar1]" Property="Value"/>
			</Group>
			<!-- ... -->
			<!-- Derived calculations -->
			<Group>
				<Output xsi:type="ExpressionCalculator" Output="Variable" Name="[DerivedVar]" Expression="[AggVar1] + [AggVar2]" Format="0.00"/>
			</Group>
			<!-- Final row rendering -->
			<Group>
				<!-- Optional: accumulate running totals for the footer -->
				<Output xsi:type="RenderValue" Output="VariableSum" Name="[TotalAgg1]" Value="[AggVar1]"/>
				<Output xsi:type="RenderValue" Name="col" Value="[ContextVar1]"/>
				<Output xsi:type="RenderValue" Name="col" Value="[PropVar1]"/>
				<Output xsi:type="RenderValue" Name="col" Value="[AggVar1]" Format="0.00"/>
				<Output xsi:type="RenderValue" Name="col" Value="[DerivedVar]" Format="0.00"/>
				<!-- ... -->
			</Group>
		</Group>
		<!-- Optional: footer row of totals, after the Rows group -->
		<Group GroupName="Footer" ItemName="Row">
			<Output xsi:type="RenderValue" Name="col" Value=""/>
			<Output xsi:type="RenderValue" Name="col" Value="Total"/>
			<Output xsi:type="RenderValue" Name="col" Value="[TotalAgg1]" Format="0.00"/>
			<!-- ... one col per header, in the same order -->
		</Group>
	</Groups>
</Query>
```
 
## Common Mistakes to Avoid

- **Referencing `[RowKey]` (or any per-row key) without declaring it.**
  A nested selector such as `/Employer/[EmployerKey]/Employee/[RowKey]/PayLines` only works when the
  Rows group declares `UniqueKeyVariable="[RowKey]"`. Without it, substitution leaves the literal text
  `[RowKey]` in the URL and the query fails.
- **Ordering or filtering in an entity-less group.**
  `<Order>` and `<Filter>` elements only act on the entities matched by *their own group's* Selector.
  A `<Group>` with no Selector matches nothing, so an Order/Filter placed there is silently ignored.
  To sort the report rows, put the `<Order>` inside the Rows group itself.
- **Rendering directly from a nested collection selector.**
  An `<Output xsi:type="RenderProperty">` inside a nested group renders once per matched entity. If the
  selector returns a collection (e.g. `/Employer/[EmployerKey]/Employee/[RowKey]/PayRuns`), the row gains
  one value per entity and the columns no longer line up with the headers. Instead capture the value into
  a variable (`Output="Variable"`) and render it from the final entity-less rendering group.
- **"Most recent X" needs order-then-take-one.**
  To fetch the latest entity from a collection, combine a descending order with a take-first filter:
  `<Order xsi:type="Descending" Property="PaymentDate" />` plus `<Filter xsi:type="TakeFirst" Value="1" />`.
  An Order alone still returns every entity.
- **Header/row column mismatch.**
  Every `col` output in the Headers group must correspond to exactly one `col` value rendered per row,
  in the same sequence. Whenever you add, remove or reorder a column, update both groups together.
  If a Footer group is present it must render the same number of `col` values, in the same order —
  use a literal blank or label for columns that carry no total.
- **Inventing nested routes.**
  Selectors must match a real GET API route. Child data is reached through its own route
  (e.g. employee pay lines are at `/Employer/{employerId}/Employee/{employeeId}/PayLines`), not by
  appending entity names to a collection URL. Verify every selector with `list_routes`.
- **Wrapping the Rows group inside an outer named group.**
  There must be exactly one `Rows`/`Row` group, and it must sit **directly under the root `<Groups>`**,
  after the Headers group. Do not enclose it in an outer group with a `GroupName` or `ItemName`
  (e.g. a `Schedules`/`Schedule` group emitting one section per schedule). That wrapper adds its own
  container elements around every row, producing `Table > Schedules > Schedule > Rows > Row`. The
  tabular consumers (CSV export, the report table) read rows from a single root-level `Rows` group, so
  the nested shape yields headers with no rows and an empty export. To vary rows by an outer entity,
  fold that entity into the `Rows` selector, or iterate it in an **un-named** `<Group>` (no `GroupName`
  or `ItemName`) that captures the outer key into a variable which the `Rows` selector then references.

This fundamental tubular output pattern is highly reusable across different reporting scenarios by adjusting: 
  
* The filter variables for scope control.
* The header values to match desired output columns.
* The selectors and nested groups to reflect the data hierarchy.
* The variables and aggregation logic to compute necessary metrics.
* The final output order to align with headers.