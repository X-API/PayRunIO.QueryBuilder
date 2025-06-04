# Abstract Guide to the Tubular Output Pattern in RQL

## Conceptual Overview

This pattern produces a tabular report as an XML or JSON response, structured into:
* Top-level filter variables controlling the data scope.
* Static header row defining column names.
* Data rows, each representing an entity instance with values aggregated and formatted.
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
 

## Execution Flow Summary
- Initialize filter variables to define query scope.
- Output header row statically.
- Load contextual entities and extract metadata into variables.
- Iterate over main data collection (rows):
 * Reset aggregation variables.
 * Capture entity properties.
 * Query nested child entities with filters/predicates.
 * Aggregate values into variables.
 * Calculate derived metrics.
 * Render all values in order as a single tabular row.
 

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
				<Output xsi:type="RenderValue" Name="col" Value="[ContextVar1]"/>
				<Output xsi:type="RenderValue" Name="col" Value="[PropVar1]"/>
				<Output xsi:type="RenderValue" Name="col" Value="[AggVar1]" Format="0.00"/>
				<Output xsi:type="RenderValue" Name="col" Value="[DerivedVar]" Format="0.00"/>
				<!-- ... -->
			</Group>
		</Group>
	</Groups>
</Query>
```
 
This fundamental tubular output pattern is highly reusable across different reporting scenarios by adjusting: 
  
* The filter variables for scope control.
* The header values to match desired output columns.
* The selectors and nested groups to reflect the data hierarchy.
* The variables and aggregation logic to compute necessary metrics.
* The final output order to align with headers.