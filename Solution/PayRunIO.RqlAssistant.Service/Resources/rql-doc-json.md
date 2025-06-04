---
title: Report Query Language
description:
position: 950
---
# Report Query Language

## 📚 Table of Contents

- [Creating your first query](#creating-your-first-query)
  - [Query  ](#query--)
  - [Root Node Name  ](#root-node-name--)
  - [Groups  ](#groups--)
  - [What is an Output?  ](#what-is-an-output--)
- [Going Deeper: Using Nested Groups  ](#going-deeper-using-nested-groups--)
  - [The Unique Key Variable  ](#the-unique-key-variable--)
  - [Understanding Resource Scope  ](#understanding-resource-scope--)
- [Structural Overview: RQL Query Anatomy](#structural-overview-rql-query-anatomy)
  - [RQL XML Structure (Hierarchy Tree)](#rql-xml-structure-hierarchy-tree)
  - [Element Summary](#element-summary)
  - [Notes for Tooling and LLMs](#notes-for-tooling-and-llms)
- [Groups  ](#groups--)
- [Selectors](#selectors)
  - [Syntax Overview](#syntax-overview)
  - [Selector Behavior](#selector-behavior)
  - [Examples](#examples)
  - [Special Cases](#special-cases)
- [Outputs  ](#outputs--)
  - [Output Render Types  ](#output-render-types--)
    - [Query using different output render types. ](#query-using-different-output-render-types-)
    - [Example of output render types query results  ](#example-of-output-render-types-query-results--)
  - [The Output Options](#the-output-options)
  - [Output Examples ](#output-examples-)
    - [Array Hint Output](#array-hint-output)
    - [Render Constant](#render-constant)
    - [Render Date Add](#render-date-add)
    - [Render Entity](#render-entity)
    - [Render Index](#render-index)
    - [Render Link](#render-link)
    - [Render Next Date](#render-next-date)
    - [Render Property](#render-property)
    - [Render Tax Period Date](#render-tax-period-date)
    - [Render Tax Period](#render-tax-period)
    - [Render Type Name](#render-type-name)
    - [Render Unique Key From Link](#render-unique-key-from-link)
    - [Render Value](#render-value)
    - [Avg](#avg)
    - [Count](#count)
    - [Distinct](#distinct)
    - [Expression Calculator](#expression-calculator)
    - [Max](#max)
    - [Min](#min)
    - [Sum](#sum)
- [Variables  ](#variables--)
  - [Example: Variable use within query](#example-variable-use-within-query)
    - [Example Response](#example-response)
  - [Variable Substitution](#variable-substitution)
  - [Syntax and Naming](#syntax-and-naming)
    - [Substitution Locations](#substitution-locations)
    - [Evaluation and Matching Rules](#evaluation-and-matching-rules)
    - [Variable Lifecycle and Scope](#variable-lifecycle-and-scope)
    - [Variable Lifecycle Caveat: Leakage Between Iterations](#variable-lifecycle-caveat-leakage-between-iterations)
    - [Reserved Variable Names](#reserved-variable-names)
    - [Example: Substitution in Action](#example-substitution-in-action)
- [Conditions and Conditional Group Logic](#conditions-and-conditional-group-logic)
  - [Available Condition Types](#available-condition-types)
  - [Condition Examples](#condition-examples)
    - [WhenContains](#whencontains)
    - [WhenEqualTo](#whenequalto)
    - [WhenGreaterThan](#whengreaterthan)
    - [WhenGreaterThanOrEqualTo](#whengreaterthanorequalto)
    - [WhenLessThan](#whenlessthan)
    - [WhenLessThanOrEqualTo](#whenlessthanorequalto)
    - [WhenNotContains](#whennotcontains)
    - [WhenNotEqualTo](#whennotequalto)
    - [WhenNotWithinArray](#whennotwithinarray)
    - [When](#when)
    - [WhenNot](#whennot)
    - [WhenWithinArray](#whenwithinarray)
  - [Condition Timing and Evaluation Summary](#condition-timing-and-evaluation-summary)
    - [Evaluation Order Recap](#evaluation-order-recap)
    - [What Is Skipped](#what-is-skipped)
    - [No `ElseGroup` Support](#no-elsegroup-support)
- [Filters  ](#filters--)
    - [Filter Example 1](#filter-example-1)
    - [Filter Example 2](#filter-example-2)
  - [Filter Type Examples  ](#filter-type-examples--)
    - [ActiveOn](#activeon)
    - [ActiveWithin](#activewithin)
    - [Between](#between)
    - [Contain](#contain)
    - [EndsWith](#endswith)
    - [EqualTo](#equalto)
    - [GreaterThan](#greaterthan)
    - [GreaterThanEqualTo](#greaterthanequalto)
    - [HasFlag](#hasflag)
    - [IsNotNull](#isnotnull)
    - [IsNull](#isnull)
    - [LessThan](#lessthan)
    - [LessThanEqualTo](#lessthanequalto)
    - [NotContain](#notcontain)
    - [NotEqualTo](#notequalto)
    - [NotHasFlag](#nothasflag)
    - [NotOfType  ](#notoftype--)
    - [NotWithinArray  ](#notwithinarray--)
    - [OfType  ](#oftype--)
    - [StartsWith](#startswith)
    - [TakeFirst](#takefirst)
    - [WithinArray  ](#withinarray--)
- [Predicate vs Filter vs Condition](#predicate-vs-filter-vs-condition)
  - [Summary Comparison](#summary-comparison)
  - [In Detail](#in-detail)
    - [Predicate](#predicate)
    - [Filter](#filter)
    - [Condition](#condition)
  - [Side-by-Side Example](#side-by-side-example)
- [Ordering  ](#ordering--)
  - [Ascending  ](#ascending--)
  - [Descending  ](#descending--)
  - [Example Including Ordering ](#example-including-ordering-)
- [Loop Expressions  ](#loop-expressions--)
- [Data Types and Formatting](#data-types-and-formatting)
  - [Supported Data Types](#supported-data-types)
  - [Format Support](#format-support)
  - [Date Format Examples](#date-format-examples)
  - [Number Format Examples](#number-format-examples)
  - [Example: Formatting with RenderValue](#example-formatting-with-rendervalue)
  - [Handling Nulls and Defaults](#handling-nulls-and-defaults)
- [Output Evaluation Context](#output-evaluation-context)
  - [Output Lifecycle Overview](#output-lifecycle-overview)
  - [Variable Timing and Scope](#variable-timing-and-scope)
  - [Skipped Groups](#skipped-groups)
  - [Empty Match Behavior](#empty-match-behavior)
  - [Output Types and Evaluation Timing](#output-types-and-evaluation-timing)
  - [Nested Group Evaluation](#nested-group-evaluation)
  - [Loop Expression Behavior](#loop-expression-behavior)
  - [Example: Evaluation Flow](#example-evaluation-flow)
- [Advanced Features Pt.1](#advanced-features-pt1)
  - [Filtering With Or  ](#filtering-with-or--)
  - [Wildcard URL support  ](#wildcard-url-support--)
  - [Direct DB Predicates  ](#direct-db-predicates--)
    - [Predicate Patterns  ](#predicate-patterns--)
    - [Operators  ](#operators--)
    - [Special operators](#special-operators)
    - [CONTAINS Example  ](#contains-example--)
    - [EXCLUDES Example  ](#excludes-example--)
    - [OFTYPE Example  ](#oftype-example--)
    - [Data types](#data-types)
  - [Regular Expression Support  ](#regular-expression-support--)
- [Advanced Features Pt.2](#advanced-features-pt2)
  - [Optimised Query Groups](#optimised-query-groups)
  - [How Optimisation Works](#how-optimisation-works)
  - [What's Included and Excluded](#whats-included-and-excluded)
  - [Optimisation Limitations](#optimisation-limitations)
    - [Example: Plural Type Endpoint](#example-plural-type-endpoint)
- [Advanced Techniques](#advanced-techniques)
  - [Use Variable Assignment to Control Output](#use-variable-assignment-to-control-output)
    - [Example: Query With Incorrect Output Order](#example-query-with-incorrect-output-order)
    - [Example Output of Incorrect Query](#example-output-of-incorrect-query)
    - [Example: Query Using Variable Assignment to Correct Output Order](#example-query-using-variable-assignment-to-correct-output-order)
    - [Example Output of Correct Query](#example-output-of-correct-query)


Another feature of the API is reporting. By default, every account setup includes a suite of statutory reports that can be executed against your data. These reports represent some common payroll requirements and cannot be altered. But that is just a small aspect of the query engine…  

The API includes a fully featured query language that allows you to create your own custom reports. Reports are saved queries that can be executed with variable values and are free to create as needed.  

There is also an API for executing ad-hoc queries without the need to create a report definition. This API allows you to post a query payload and returns the results in your chosen language.  

This tutorial will focus on ad-hoc queries, but this also relates to custom report definitions.  

See [Report Query Language Reference](/docs/reference/query/index.html) for more information on the RQL implementation.  
  
## Creating your first query

The query language works with the API resource locators to allow you to select entities to report on. By using groups, outputs, filters and conditions, it is possible to retrieve entire hierarchies of data in a single transaction.  
Here’s an example query that returns all employer entities within the application scope.

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "SimpleQuery",
    "Groups": {
      "Group": {
        "@GroupName": "Employers",
        "@ItemName": "Employer",
        "@Selector": "/Employers",
        "Output": [
          {
            "@xsi:type": "RenderEntity"
          }
        ]
      }
    }
  }
}
```
--/CodeTabs--

Let’s examine the query piece by piece…

### Query  

This is the root element of the query definition.  

### Root Node Name  

The first thing a query needs is the name of the root result element. This must be an alphanumeric string value without spaces or special characters.  

### Groups  
  
A collection of one or more reporting groups.  

:::info
**What is a Group?**  
The group is the core element of the query. It allows you to specify the resource location of the entity (or entities) to be queried. Groups are also used to structure the query response; by using the “Group” and “Item” name attributes you can control the response hierarchy. 
:::

**Core Group Properties:**  

* Group Name – When specified, this value creates a parent entity over any matched results.  
* Item Name –  When specified, this value provides the name for matched entity nodes.  
* Selector – The URL path to target an entity or collection of entities.  
  
### What is an Output?  

Groups are for selecting resources and outputs are for rendering responses. There are many different types of output, denoted by the “xsi:type” value.  
In the above simple example; an output of type “RenderEntity” is specified. This output type generates a response representing the entire matched entity.  
  
**Example Query Response**  
When the simple query is executed, the query processor will find all entities that match the specified group selector.

--CodeTabs--

```json
{
  "SimpleQuery": {
    "@Generated": "2017-08-15T14:28:25",
    "Employers": {
      "Employer": [
        {
          "EffectiveDate": "0001-01-01",
          "Revision": "1",
          "Name": "Employer A",
          "Region": "NotSet",
          "Territory": "NotSet",
          "RuleExclusions": "None",
          "ClaimEmploymentAllowance": "false",
          "ClaimSmallEmployerRelief": "false",
          "ApprenticeshipLevyAllowance": "0.0000",
          "HmrcSettings": {
            "TaxOfficeNumber": "123",
            "TaxOfficeReference": "ABC123",
            "AccountingOfficeRef": "ABCP1234567",
            "Sender": "Employer"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--  

## Going Deeper: Using Nested Groups  
  
Query groups have another useful feature: they can be nested. When groups are nested, the query response structure mirrors the specified nested group hierarchy.  
An example of nested group query.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "NestedGroupQuery",
    "Groups": {
      "Group": {
        "@GroupName": "Employers",
        "@ItemName": "Employer",
        "@Selector": "/Employers",
        "@UniqueKeyVariable": "[EmployerKey]",
        "Group": {
          "@GroupName": "Employees",
          "@ItemName": "Employee",
          "@Selector": "/Employer/[EmployerKey]/Employees",
          "Output": {
            "@xsi:type": "RenderEntity"
          }
        }
      }
    }
  }
}
```
--/CodeTabs--
  
This example has two levels of grouping. Level 1 selects all employers and the level 2 selects all employees within the parent employer.  

**Example Query Results**  

--CodeTabs--

```json
{
  "NestedGroupQuery": {
    "@xmlns:xsi": "http://www.w3.org/2001/XMLSchema-instance",
        "@Generated": "2017-08-15T15:00:53",
    "Employers": {
      "Employer": {
        "Employees": {
          "Employee": [
            {
              "EffectiveDate": "2016-04-01",
              "Revision": "1",
              "Code": "CP101",
              "FirstName": "John",
              "LastName": "Bloggs",
              "DateOfBirth": "1970-01-01",
              "Directorship": "Off",
              "Gender": "Male",
              "NicLiability": null,
              "Region": "England",
              "Territory": "UnitedKingdom",
              "StartDate": "0001-01-01",
              "StarterDeclaration": {
                "@xsi:nil": "true"
              },
              "LeavingDate": {
                "@xsi:nil": "true"
              },
              "LeaverReason": {
                "@xsi:nil": "true"
              },
              "RuleExclusions": "None",
              "WorkingWeek": "None",
              "HoursPerWeek": "0.0000",
              "Seconded": "NotSet",
              "EEACitizen": "false",
              "EPM6": "false",
              "PaymentToANonIndividual": "false",
              "IrregularEmployment": "false",
              "OnStrike": "false",
              "PaymentMethod": "NotSet",
              "IsAgencyWorker": "false"
            },
            {
              "EffectiveDate": "2016-04-01",
              "Revision": "1",
              "Code": "CP102",
              "FirstName": "Jane",
              "LastName": "Bloggs",
              "DateOfBirth": "1980-01-01",
              "Directorship": "Off",
              "Gender": "Female",
              "NicLiability": null,
              "Region": "England",
              "Territory": "UnitedKingdom",
              "StartDate": "0001-01-01",
              "StarterDeclaration": {
                "@xsi:nil": "true"
              },
              "LeavingDate": {
                "@xsi:nil": "true"
              },
              "LeaverReason": {
                "@xsi:nil": "true"
              },
              "RuleExclusions": "None",
              "WorkingWeek": "None",
              "HoursPerWeek": "0.0000",
              "Seconded": "NotSet",
              "EEACitizen": "false",
              "EPM6": "false",
              "PaymentToANonIndividual": "false",
              "IrregularEmployment": "false",
              "OnStrike": "false",
              "PaymentMethod": "NotSet",
              "IsAgencyWorker": "false"
            }
          ]
        }
      }
    }
  }
}
```
--/CodeTabs--

### The Unique Key Variable  

If you examine the nested group query example above, you will notice a property named: “UniqueKeyVariable” which is set to “[EmployerKey]”.  

--CodeTabs--

--/CodeTabs--
  
This property allows you to define a variable that will be populated with the unique key of the resource in scope.  
Every resource [in the API] has an identity value that is unique within the scope of the parent entity. This value is known as the unique key. The keys are used in the API URL path to identify unique resources.  
By setting the “UniqueKeyVariable”, the key of the matched resource is captured and stored in a variable with a name matching the specified value. In this example, the variable is named: “[EmployerKey]”.  
Because the “selector” group property allows for variable substitution, you can then use the “[EmployerKey]” variable in place of the employer’s unique key with in nested sub groups.  
  
Consider this example:  

Your application includes 2 employers; ER001 and ER002. When the query is processed, the level 1 group matches ER001 first, ER001 is inserted into the “[EmployerKey]” variable.  
The process execution continues and moves to the nested level 2 group. Variable substitution is performed on the selector value, resulting in a resource locator of “/Employer/ER001/Employees”.  
The inner group is processed and the execution returns to the second matched employer: ER002. Once again key is captured and variable substitution is performed. This time the selector value is set to “/Employer/ER002/Employees”.  

1. Nested group selector: /Employer/[EmployerKey]/Employees
2. In first loop becomes: /Employer/ER001/Employees
3. In the second loop: /Employer/ER002/Employees  
  
Using this technique, it is possible to nest multiple levels of groups, building up the resource locator as you go and avoid hard coded unique key values.  
Note: To be used in variable substitution, variable names must be wrapped in square brackets “[Example]”.  

### Understanding Resource Scope  

The query processor traverses the resources in a looping fashion. Where you have a group that matches multiple resources, each resource is placed in-scope while the group’s outputs are processed. This is repeated for each matched resource.  
Where the group has a sub group, the child groups are processed in sequence before the scope returns to the next matched parent entity.  
  
**Example Resources in Scope:**  

The top level group matches 2 employers, each employer has 2 employees matched by a level 2 sub group.

1. Start  
2. Employer 1 in scope  
   a.    Employer 1 - Employee A in scope  
   b.    Employer 1 - Employee B in scope  
3. Employer 2 in scope  
    a.    Employer 2 - Employee A in scope  
    b.    Employer 2 - Employee B in scope  
4. End  

## Structural Overview: RQL Query Anatomy

This section provides a structural overview of the RQL query format. It shows how each element fits into the overall hierarchy and what children are valid at each level.

---

### RQL XML Structure (Hierarchy Tree)

```
<Query>
├── <RootNodeName>                  # Single required node name
├── <Variables>                     # Optional collection of variables
│   └── <Variable @Name @Value />
└── <Groups>                        # Required block of reporting groups
    └── <Group>                     # One or more nested groups
        ├── @GroupName              # Optional label for output nesting
        ├── @ItemName               # Optional label for repeated items
        ├── @Selector               # Optional URI path to data
        ├── @UniqueKeyVariable      # Optional variable assignment for unique key
        ├── @Predicate              # Optional pre-fetch filter expression
        ├── <Conditions>            # Optional condition logic
        │   └── <Condition>         # One or more <When> or <WhenNot> rules
        ├── <Filters>               # Optional filters applied after fetch
        │   └── <Filter xsi:type=... />
        ├── <Output>                # Optional output instructions
        │   └── <Output xsi:type=... />
        ├── <Ordering>              # Optional ordering instructions
        │   └── <OrderBy @Property />
        └── <Group>                 # Recursively nested subgroups
```

---

### Element Summary

| Element             | Description                                                      | Children                                     |
|---------------------|------------------------------------------------------------------|----------------------------------------------|
| `<Query>`           | Root query container                                             | RootNodeName, Variables, Groups              |
| `<RootNodeName>`    | Defines the root element of the resulting XML                    | None                                         |
| `<Variables>`       | Declares constants or runtime-assigned values                    | `<Variable>`                                 |
| `<Variable>`        | A key-value pair, used for substitution or logic                 | None                                         |
| `<Groups>`          | Contains one or more `<Group>` blocks                            | `<Group>`                                    |
| `<Group>`           | Selects and operates on a set of API resources                   | Conditions, Filters, Output, Ordering, Group |
| `<Conditions>`      | Determines if the group should execute                           | `<Condition>`                                |
| `<Filters>`         | Post-fetch filtering on matched entities                         | `<Filter>`                                   |
| `<Output>`          | Declares one or more outputs (e.g., value, property, variable)   | None or repeated                             |
| `<Ordering>`        | Sorts the matched entities by one or more fields                 | `<OrderBy>`                                  |
| `<OrderBy>`         | Defines a property and direction for ordering                    | None                                         |

---

### Notes for Tooling and LLMs

- Each `<Group>` may recursively contain additional `<Group>` elements.
- `<Variables>` are globally accessible throughout the query after declaration.
- `<Predicate>` is a group attribute and used **before** data retrieval.
- `<Filter>` elements act **after** entity fetch.
- `<Condition>` blocks allow `<When>` / `<WhenNot>` clauses for toggling group presence.
- Every `<Output>` element should have a `xsi:type` that defines its function.

---

This structure can be used as a reference when authoring queries or when building tools that support or interpret RQL.

## Groups  
  
Groups form the query output hierarchy. Including attributes that allow you specify the output node names and URI path selector.  
Groups also support nested elements used to filter, aggregate, render and order the results.  
  
Attributes:  

* Group name - The title element name of the grouped section.
* Item name - The name of each sub element, repeated for each matched entity
* Selector - The URL pattern used to match the API entities
* Unique key variable - The variable name populated with the unique resource location key of the match entity in scope
* Loop expression - An expression used to repeat the group while inserting a loop variable value
  
Group child collections:  

* Groups - Additional sub groups repeated for each matched entity in the parent group
* Conditions - Boolean decisions used to determine if the group should be rendered
* Filters - Filtering conditions applied to the matched entities
* Outputs - Instructions on what is to be written to the generated report output
* Ordering - Determines which fields of the matched entities are used to order the results 

## Selectors

The `@Selector` attribute defines the **entity or collection** that a `<Group>` will operate on. It behaves similarly to a RESTful URI path and is used to access nested resources in the payroll API.

Selectors determine **what data gets retrieved**, before filters or output generation occurs.

---

### Syntax Overview



- Starts with `/` and references a known API path
- Can include **variable substitution** using square brackets (e.g. `[EmployerKey]`)
- Can also be **hard-coded directly**, such as `/Employer/ER001/Employee/EE001/PayLines`
- Supports deep traversal through hierarchical entities

---

### Selector Behavior

| Characteristic         | Description                                                        |
|------------------------|--------------------------------------------------------------------|
| **Required**           | Every `<Group>` must have a `Selector` unless it’s a static node   |
| **Variable Support**   | Variables may be substituted using `[VarName]`, but are **optional**|
| **Data Scope**         | Acts as the input to predicates, filters, and outputs              |

> If the `Selector` yields no matches, the group (and its children) are skipped.

---

### Examples

--CodeTabs--

--/CodeTabs--

---

### Special Cases

- **Optional Selector**: If a `<Group>` omits the `Selector`, it will still be processed. This is useful for generating headers, summaries, or wrapper nodes.
- **Looping Contexts**: When combined with `LoopExpression`, the `Selector` is re-evaluated per loop iteration using `[LoopVariable]`.

--CodeTabs--

--/CodeTabs--

---

Selectors define the shape and origin of data within your query. Whether hardcoded or dynamic, they form the foundation of RQL’s data pipeline.

## Outputs  
  
The job of outputs is to capture result values. These values can come from properties of the entity in scope, variables or aggregations of all group matches. The output object also allows you to specify how the value is expressed.  
  
The value of an output can be:  

* Rendered into a result element
* Rendered into a result attribute
* Inserted into a variable
* Summed with an existing variable
* Appended to a variable  
  
There are 2 categories of outputs; singular and aggregates.  
  
Single value outputs only consider the entity in scope. They are applied for each entity matched by the group selector.  
  
Examples of single value output are:  
  
* Render entity
* Render link
* Render property
* Render value  
  
Aggregate value outputs consider all entities matched by the group selector. This is useful for applying aggregation expressions over a collection of matched resources. For example: summing the net pay value for all an employee’s pay lines.  
  
Examples of aggregate outputs are:  

* Average
* Max
* Min
* Sum
* Count
* Expression calculator  
  
The expression calculator supports simple arithmetic of constants and variable numbers using +, -, / and *.  
  
Outputs also support formatting expressions. For example:  

* “yyyy-mm-dd" for sortable dates
* “0.00” for fixed precision numbers  

### Output Render Types  

All entity group rendering outputs allow for the specification of an **Output Render Type**. This controls the behaviour of where rendered output value will be written.  

| Output Type     | Behaviour                                                                                      |
|-----------------|------------------------------------------------------------------------------------------------|
| Element         | Indicates the output should be written to an element.                                          | 
| Attribute       | Indicates the output should be written to an attribute.                                        |
| Variable        | Indicates the output should be written to a variable.                                          |
| VariableSum     | Indicates the output should be summed to the current variable value. Note: Numeric values only |
| VariableAppend  | Indicates the output should be joined to the end of current variable value.                    |
| VariablePrepend | Indicates the output should be joined to the start of the current variable value.              |
| InnerText       | Indicates that the value should be rendered to the inner text of the node in context.          |

> Note: If not specified; the **Element** output render type is used.  

#### Query using different output render types. 

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputTypeExamples",
    "Groups": {
      "Group": [
        {
          "@GroupName": "Output",
          "Output": [
            {
              "@xsi:type": "RenderValue",
              "@Name": "MyValue",
              "@Value": "Hard Coded Value"
            },
            {
              "@xsi:type": "RenderValue",
              "@Output": "Attribute",
              "@Name": "MyAttribute",
              "@Value": "AttributeValue"
            },
            {
              "@xsi:type": "RenderValue",
              "@Output": "Variable",
              "@Name": "[MyVariable]",
              "@Value": "ValueA"
            },
            {
              "@xsi:type": "RenderValue",
              "@Output": "VariableAppend",
              "@Name": "[MyVariable]",
              "@Value": "-ValueB"
            },
            {
              "@xsi:type": "RenderValue",
              "@Output": "VariablePrepend",
              "@Name": "[MyVariable]",
              "@Value": "ValueC-"
            },
            {
              "@xsi:type": "RenderValue",
              "@Output": "VariablePrepend",
              "@Name": "[MyVariable]",
              "@Value": "ValueC-"
            }
          ],
          "Group": {
            "@GroupName": "Element",
            "Output": {
              "@xsi:type": "RenderValue",
              "@Output": "InnerText",
              "@Name": "-",
              "@Value": "Inner text goes here"
            }
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Example of output render types query results  

--CodeTabs--

```json
{
  "OutputTypeExamples": {
    "@Generated": "2025-05-22T17:01:12",
    "@Duration": "00:00:00.0000781",
    "Output": {
      "@MyAttribute": "AttributeValue",
      "MyValue": "Hard Coded Value",
      "MyVariable": "ValueC-ValueA-ValueB",
      "Element": "Inner text goes here"
    }
  }
}
```
--/CodeTabs--


### The Output Options

| Output Name             | Category  | Description                                                                       |
|-------------------------|-----------|-----------------------------------------------------------------------------------|
| RenderArrayHint         | Singular  | Instructs the report generator to force the use of JSON array brackets            |
| RenderConstant          | Singular  | Outputs a built in API constant value                                             |
| RenderDateAdd           | Singular  | Outputs a date value based on the specified chronological increment               |
| RenderEntity            | Singular  | Output the entire matched entity                                                  |
| RenderIndex             | Singular  | Render the incrementing "index" attribute value                                   |
| RenderLink              | Singular  | Output a link element targeting the in-scope entity                               |
| RenderNextDate          | Singular  | Output a date incremented by the specified pay frequency                          |
| RenderProperty          | Singular  | Output the specified property value of the in-scope entity                        |
| RenderTaxPeriodDate     | Singular  | Output the taxation reporting period dates based on the specified contextual date |
| RenderTaxPeriod         | Singular  | Output a string representation of the tax period indicated by the specified date  |
| RenderTypeName          | Singular  | Output the type name of the entity in-scope                                       |
| RenderUniqueKeyFromLink | Singular  | Given a API link, output the unique key element                                   |
| RenderValue             | Singular  | Output the value indicated                                                        |
| Avg                     | Aggregate | Output the average value of the specified property in the entity group            |
| Count                   | Aggregate | Output the count of matched entities in the entity group                          |
| Distinct                | Aggregate | Only output distinct values of the specified property in the entity group         |
| ExpressionCalculator    | Aggregate | Use simple arithmetic to calculate numeric values                                 |
| Max                     | Aggregate | Output the maximum value of the specified property in the entity group            |
| Min                     | Aggregate | Output the minimum value of the specified property in the entity group            |
| Sum                     | Aggregate | Output the sum value of the specified property in the entity group                |

### Output Examples 

The following section includes examples of all the output types.  

#### Array Hint Output

The array hint output is used to ensure that generated JSON output includes array bracket indicators.

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderArrayHint"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Constant

The **RenderConstant** output is used to pull constant fixed values from the API for use in query output.  

:::danger  
**Render Constant Requires Context Date Variable**  
Please note: In order to use the **RenderConstant** query, there must be a valid date "[ContextDate]" variable set.  
:::

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Variables": {
      "Variable": [
        {
          "@Name": "[ContextDate]",
          "@Value": "2025-05-22"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderConstant",
            "@ConstantName": "ApprenticeshipLevyPercentage",
            "@ConstantType": "System.DateTime",
            "@Name": "Example"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--


#### Render Date Add

The **RenderDateAdd** output type is used to calculate a date offset using a number of chronological increments. The increment amount must be an integer value. 
Use a positive increment for future dates and negative increment for past dates.  

:::success
**Interval options**  
You can specify either: Day, Month or Year intervals.  
:::

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderDateAdd",
            "@Name": "DateAddExample",
            "@Date": "2025-05-22",
            "@Interval": "Day",
            "@Increment": "1",
            "@Format": "yyyy-MM-dd"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Entity

Use the **RenderEntity** output to include the entire entity details in the query output.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderEntity"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Index

You can use **RenderIndex** to include an auto incremented index value output into the response stream. 
This feature also enables paged responses when specifying the "[StartIndex]" and "[MaxIndex]" variable arguments.  

> Note: Use of "[StartIndex]" and "[MaxIndex]" are optional. Omission returns all results.   

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Variables": {
      "Variable": [
        {
          "@Name": "[StartIndex]",
          "@Value": "1"
        },
        {
          "@Name": "[MaxIndex]",
          "@Value": "10"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderIndex"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Link

The **RenderLink** output instructs the query response generator to include a link element in the rendered response.  

:::success
**Render Link Variable Behaviour**  
The **RenderLink** output includes special behaviour when the output type is set to "Variable".  
Variable outputs are written to a special variable named **[Link]**  
:::

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderLink"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Next Date

You can use the **RenderNextDate** to calculate the next date based on a payment frequency.  

The available pay frequencies are as follows:  
  
* Weekly
* Monthly
* Quarterly
* Biannually
* TwoWeekly
* FourWeekly
* Yearly

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderNextDate",
            "@Name": "NextDateExample",
            "@Date": "2025-05-22",
            "@PayFrequency": "Weekly",
            "@Format": "yyyy-MM-dd"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Property

The **RenderProperty** output allows you to specify a property (of the entity in-scope) to be rendered to the query output.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderProperty",
            "@Name": "PropertyExample",
            "@Property": "FirstName"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Tax Period Date

The **RenderTaxPeriodDate** enables the output of dates relative to UK taxation periods.  
To use this output you must specify the tax year, tax period number and pay frequency type.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderTaxPeriodDate",
            "@DisplayName": "TaxPeriodStartDate",
            "@TaxYear": "2025",
            "@TaxPeriod": "1",
            "@PayFrequency": "Monthly",
            "@Format": "yyyy-MM-dd"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Tax Period

Use the **RenderTaxPeriod** output to include the tax period details as a formatted expression.  
  
The output provides 3 render options:  
  
* RenderOption: **AsString** (default) - Example: _W06-2025-26_
* RenderOption: **PeriodOnly** - Example: 6
* RenderOption: **YearOnly** - Example: 2025

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": [
            {
              "@xsi:type": "RenderTaxPeriod",
              "@DisplayName": "TaxPeriodAsString",
              "@PayFrequency": "Monthly",
              "@Date": "2025-05-22"
            },
            {
              "@xsi:type": "RenderTaxPeriod",
              "@DisplayName": "TaxPeriodPeriodOnly",
              "@PayFrequency": "Weekly",
              "@Date": "2025-05-22",
              "@RenderOption": "PeriodOnly"
            },
            {
              "@xsi:type": "RenderTaxPeriod",
              "@DisplayName": "TaxPeriodYear",
              "@PayFrequency": "FourWeekly",
              "@Date": "2025-05-22",
              "@RenderOption": "YearOnly"
            }
          ]
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Type Name

The **RenderTypeName** outputs the type name of the entity in-scope. Useful when iterating over result sets that contain multiple entity types.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderTypeName",
            "@Name": "Name"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Unique Key From Link

The **RenderUniqueKeyFromLink** is used to identify and output the unique key aspect of a specified hyperlink reference.  
Often used in conjunction with the **RenderLink** to variable output.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "RenderUniqueKeyFromLink",
            "@Name": "Name",
            "@Href": "/Employer/ER001/Employee/EE001"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Render Value

The **RenderValue** output provides a mechanism to output hard coded and variable values.  
This powerful output type supports some additional features used to manipulate the generated values.  

:::success
**Negate**  
Setting the _Negate_ option of the **RenderValue** output will force the output value to be inverted.  
For example: _123.45_ would be output as _-123.45_  
:::

:::success
**Regular Expression**  
You can use a regular expression in the **RenderValue** output to control string values written to the output.  
For example: Given value of "/Employer/ER001/PaySchedule/SCH001" and a **Regex** "(?<=Employer\\/)[^\\/]+" the output would be "ER001"  
:::

:::success
**Supports multiple variable substitution**  
You can specify multiple variables along with hard code text within the **RenderValue** value attribute.  
For example: Value="[VariableA] and [VariableB]"  
:::

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": [
            {
              "@xsi:type": "RenderValue",
              "@Name": "ExampleA",
              "@Value": "Hard Coded Value"
            },
            {
              "@xsi:type": "RenderValue",
              "@Name": "ExampleB",
              "@Value": "[MyVariable]"
            },
            {
              "@xsi:type": "RenderValue",
              "@Name": "ExampleC",
              "@Value": "/Employer/ER001/PaySchedule/SCH001",
              "@Regex": "(?<=Employer\\/)[^\\/]+"
            },
            {
              "@xsi:type": "RenderValue",
              "@Name": "ExampleD",
              "@Value": "123.45",
              "@Negate": "true"
            }
          ]
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Avg

The **Avg** aggregation output will render the average value of all properties within the selected entity group set. 

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "Avg",
            "@Name": "Example",
            "@Property": "Value"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Count

The **Count** aggregation output renders the number of entities matched in the current entity group set.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "Count",
            "@Name": "Name"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Distinct

You can use the **Distinct** aggregation value to only output unique values within the matched entity group set.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "Distinct",
            "@Name": "Example",
            "@Property": "LeavingDate",
            "@Format": "yyyy-MM-dd"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Expression Calculator

The **ExpressionCalculator** aggregation output allows you to perform simple arithmetic calculations on hard coded numeric values and variables.  
  
> **Note**: Calculations are performed on a next in sequence basis and only support addition, subtraction, multiplication and division.
  
You can also set optional **Max** and **Min** values.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Variables": {
      "Variable": [
        {
          "@Name": "[ValueA]",
          "@Value": "99.00"
        },
        {
          "@Name": "[ValueB]",
          "@Value": "0.8"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "ExpressionCalculator",
            "@Name": "Name",
            "@Format": "0.00",
            "@Expression": "[ValueA] + 1 * [ValueB]",
            "@Max": "1000.00",
            "@Min": "0",
            "@Rounding": "FiveUp"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Max

The **Max** aggregate output renders the maximum property value within the matched entities of the current selection groups.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "Max",
            "@Name": "Example",
            "@Property": "Value"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Min

The **Min** aggregate output renders the minimum property value within the matched entities of the current selection groups.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "Min",
            "@Name": "Example",
            "@Property": "Value"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--


#### Sum

The **Sum** aggregate output renders the summed value of properties within the matched entities of the current selection groups.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OutputExample",
    "Groups": {
      "Group": [
        {
          "Output": {
            "@xsi:type": "Sum",
            "@Name": "Example",
            "@Property": "Value"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

## Variables  
  
The query engine supports simple type variables. These key value pairs can be used to store strings, dates and numbers.  
Variables can be defined as constant values in the query object or can be created dynamically during query processing by using “outputs”.  
  
:::info
Variables can be named to anything, however only variables that have names wrapped in square braces [ ] can be used for variable substitution within group selector strings.  
:::

The value of a variable can be set in the "Variables" collection of the RQL statement or can be updated during the query execution.  
  
The rendered value of group output can be redirected into a variable by choosing the **Variable** output render type.  
  
Use of this feature allows for the variables to be calculated during the query execution process. 

### Example: Variable use within query

The below query performs the following actions:

- Render the parameters to output elements
- Select all employees under employer 'ER001'
- Capture the employee name details into 3 variables: [Title], [FirstName], [LastName]
- Select all employee pay lines for payment date: '2025-05-31'
- Record the sum of the pay line values into variable: [NetPay]
- Render the employee full name, payment date and Net Pay as output elements
- Render the employee unique key as an attribute
- Add the employee net pay to the [TotalNetPay] variable
- After iterating all employees, render the total pay amount into an output element.

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "7123"
        },
        {
          "@Name": "[PaymentDate]",
          "@Value": "2025-03-01"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "Parameters",
          "Output": [
            {
              "@xsi:type": "RenderValue",
              "@Name": "EmployerKey",
              "@Value": "[EmployerKey]"
            },
            {
              "@xsi:type": "RenderValue",
              "@Name": "PaymentDate",
              "@Value": "[PaymentDate]",
              "@Format": "yyyy-MM-dd"
            }
          ]
        },
        {
          "@GroupName": "Employees",
          "@ItemName": "Employee",
          "@Selector": "/Employer/[EmployerKey]/Employees",
          "@UniqueKeyVariable": "[EmployeeKey]",
          "Output": [
            {
              "@xsi:type": "RenderProperty",
              "@Output": "Variable",
              "@Name": "[Title]",
              "@Property": "Title"
            },
            {
              "@xsi:type": "RenderProperty",
              "@Output": "Variable",
              "@Name": "[FirstName]",
              "@Property": "FirstName"
            },
            {
              "@xsi:type": "RenderProperty",
              "@Output": "Variable",
              "@Name": "[LastName]",
              "@Property": "LastName"
            }
          ],
          "Group": [
            {
              "@Selector": "/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines",
              "@Predicate": "PaymentDate = [PaymentDate]",
              "Output": {
                "@xsi:type": "Sum",
                "@Output": "Variable",
                "@Name": "[NetPay]",
                "@Property": "Value"
              }
            },
            {
              "Output": [
                {
                  "@xsi:type": "RenderValue",
                  "@Name": "FullName",
                  "@Value": "[Title]. [FirstName] [LastName]"
                },
                {
                  "@xsi:type": "RenderValue",
                  "@Name": "NetPay",
                  "@Value": "[NetPay]",
                  "@Format": "0.00"
                },
                {
                  "@xsi:type": "RenderValue",
                  "@Output": "Attribute",
                  "@Name": "Key",
                  "@Value": "[EmployeeKey]"
                },
                {
                  "@xsi:type": "RenderValue",
                  "@Output": "VariableSum",
                  "@Name": "[TotalNetPay]",
                  "@Value": "[NetPay]"
                }
              ]
            }
          ]
        },
        {
          "@GroupName": "Totals",
          "Output": {
            "@xsi:type": "RenderValue",
            "@Name": "NetPay",
            "@Value": "[TotalNetPay]",
            "@Format": "0.00"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Example Response

--CodeTabs--

```json
{
  "ResultSet": {
    "@Generated": "2025-05-23T11:27:10",
    "@Duration": "00:00:00.0096725",
    "Parameters": {
      "EmployerKey": "7123",
      "PaymentDate": "2025-03-01"
    },
    "Employees": {
      "Employee": [
        {
          "@Key": "288",
          "FullName": "Miss. Amy Ali",
          "NetPay": "2376.20"
        },
        {
          "@Key": "815",
          "FullName": "Mr. Amy Kemp",
          "NetPay": "2376.20"
        },
        {
          "@Key": "942",
          "FullName": "Mrs. Ann Chan",
          "NetPay": "5335.59"
        },
        {
          "@Key": "247",
          "FullName": "Miss. Ann Wood",
          "NetPay": "5335.59"
        },
        {
          "@Key": "121",
          "FullName": "Miss. Beth Day",
          "NetPay": "5335.59"
        },
        {
          "@Key": "145",
          "FullName": "Mr. Leah Cox",
          "NetPay": "920.00"
        },
        {
          "@Key": "328",
          "FullName": "Ms. Max Ross",
          "NetPay": "920.00"
        },
        {
          "@Key": "548",
          "FullName": "Mr. Sam Dyer",
          "NetPay": "920.00"
        },
        {
          "@Key": "746",
          "FullName": "Miss. Zoe Cole",
          "NetPay": "500.00"
        }
      ]
    },
    "Totals": {
      "NetPay": "24019.17"
    }
  }
}
```
--/CodeTabs--

### Variable Substitution

RQL supports runtime variable substitution, allowing query components to be dynamically constructed using predefined or computed variable values. Substitution is performed using a simple string replacement model and is applied in specific query attributes.

### Syntax and Naming

- Variables intended for substitution **must be wrapped in square brackets**, e.g. `[EmployerKey]`.
- Variable names are **case-sensitive**.
- There are **no character restrictions** on variable names, but consistency is encouraged.
- **Dollar notation** (e.g. `$Name$`) is supported in specific internal cases, but square bracket notation `[Name]` is preferred and more consistent.

#### Substitution Locations

Variable substitution occurs in the following locations:

| Area                        | Example Use                                         |
|-----------------------------|-----------------------------------------------------|
| Entity group `Selector`     | `/Employer/[EmployerKey]/Employees`                 |
| Group `Predicate` attribute | `PaymentDate = [PaymentDate]`                       |
| Filter `Value` attributes   | `<Filter xsi:type="EqualTo" Value="[Status]" />`    |
| Output `Value` attributes   | `<Output Value="[FirstName] [LastName]" />`         |
| Group `Condition` rules     | `<Condition ValueA="[ValueA]" ValueB="[ValueB]" />` |

#### Evaluation and Matching Rules

- Variables are substituted using **find-and-replace on full matches only**.
- No substitution is made if the variable is undefined — the placeholder remains as-is.
- You can safely include square brackets (`[ ]`) in non-variable contexts without triggering substitution.
- Example:

--CodeTabs--

--/CodeTabs--

#### Variable Lifecycle and Scope

- Variables can be defined statically in the `<Variables>` block or dynamically using `Output` elements with a `RenderType` of `Variable`, `VariableSum`, `VariableAppend`, etc.
- Variables have **global scope**: once defined, they are available throughout the remainder of the query processing lifecycle.
- Variables **can be overwritten** or updated multiple times.


#### Variable Lifecycle Caveat: Leakage Between Iterations

Although variables in RQL have global scope and persist through the entire query run, their values are only updated when an output actually executes. If a group that is supposed to assign or sum into a variable never matches (for example, because there are no rows to aggregate), the variable simply retains its previous value. This can lead to a **leak** of the prior iteration’s data into the next one.

As described under **Variable Lifecycle and Scope**, variables persist globally and are updated in declaration order—**but they are not automatically reset** when a subgroup doesn’t run. In practice, that means:

- If Employee A has matching pay-lines, a subgroup sums those lines and writes the result into `[NetPay]`.
- If Employee B has no pay-lines, that subgroup is skipped (no `<Output xsi:type="VariableSum">` ever executes), so `[NetPay]` remains set to Employee A’s total.
- When you finally render `[NetPay]` for Employee B, you mistakenly get Employee A’s value.

##### Scenario Illustration

Consider the existing query fragment from **Example: Variable use within query** where `[NetPay]` is only set when there are matching pay-lines:

--CodeTabs--

--/CodeTabs--

1. On the first iteration (Employee A), suppose there are pay-lines.  
   - The inner `<Group>` matches and `<Output xsi:type="Sum" Name="[NetPay]" Property="Value" />` sets `[NetPay]` to “1,200.00” (for example).  
2. On the second iteration (Employee B), suppose there are **no** pay-lines.  
   - The inner `<Group>` does not run at all, so `[NetPay]` remains at “1,200.00” from Employee A.  
3. When `<Output xsi:type="RenderValue" Name="NetPay" Value="[NetPay]" />` finally executes for Employee B, it prints “1,200.00” instead of “0”.

###### How to Avoid the Leak

The key is to **explicitly reset** `[NetPay]` to a known starting value (e.g. 0) at the top of each employee iteration—so that if the sum subgroup doesn’t run, `[NetPay]` will remain at zero. In other words, insert a “zero-assignment” output **before** the pay-line sum group runs.

Below is a revised query fragment illustrating this initialisation step:

--CodeTabs--

--/CodeTabs--

1. **Initialisation**:  
     
   Ensures `[NetPay]` is set to `0` at the very beginning of processing each `<Group>` iteration.

2. **Sum Subgroup**:  
     
   Adds to `[NetPay]` only if pay-lines exist. If there are none, `[NetPay]` remains `0`.

3. **Safe Rendering**:  
     
   Will correctly output `0.00` for employees without matching payments, rather than carrying over a previous total.

By enforcing this pattern—**always initialise any variable you intend to aggregate via `Sum`/`VariableSum` before its first use in each loop**—you prevent old data from leaking into subsequent rows. This approach applies equally to any numeric or string variable that only sometimes receives an assignment in a subgroup: if you want a fresh start each iteration, reset it first.

#### Reserved Variable Names

While RQL does not enforce reserved keywords, certain variable names are commonly used across standard templates and built-in logic. Using these helps improve clarity and consistency:

```
[TaxYear], [TaxPeriod], [TaxMonth], [TaxPeriodStart], [TaxPeriodEnd]
[PayPeriodStart], [PayPeriodEnd], [ContextDate], [LoopVariable]
[EmployerKey], [EmployeeKey], [EmployeeCodes], [PayScheduleKey]
[PayFrequency], [PayRunKey], [FromDate], [ToDate], [PensionKey]
[ItemIndex], [StartIndex], [MaxIndex], [PaymentDate], [Link]
```

These variables may be required by certain `RenderType`s or used by filters, outputs, or conditions.

#### Example: Substitution in Action



With:



Will result in:




  
## Conditions and Conditional Group Logic

Conditions in RQL are used to **dynamically include or exclude** an entire group of output logic based on comparisons between variable values or literals.

They act like **runtime `if` statements**, evaluated **before a group is processed**. If any condition fails, the entire group (and all nested elements) is skipped—ensuring no data is processed, output, or written for that group.

Each `<Condition>` performs a comparison between `ValueA` and `ValueB`, with the comparison logic determined by the `xsi:type`. This enables branching based on string values, numeric ranges, dates, or array membership.

> Conditions are useful for customizing report outputs without rewriting query structure. They are evaluated in the order they appear and do not support compound logic (AND/OR) at the element level.

Conditions are used to control whether a group (or output block) is processed, based on the current value of one or more variables. Below is a list of all supported condition types and stubs for their documentation.

All conditions follow a common logic pattern of comparing "ValueA" against "ValueB". To help understand the condition logic, the name of the condition should be considered between the two values. 
  
For example: When "ValueA" Contains "ValueB".

> The comparison values (A & B) can be either hard coded or a variable name.

---

### Available Condition Types

| Condition Type             | Description                                                | Supported data types           |
|----------------------------|------------------------------------------------------------|--------------------------------|
| `WhenContains`             | Determines if ValueA contains ValueB                       | String                         |
| `WhenEqualTo`              | Determines if ValueA is equal to ValueB                    | String, Numeric, Date, Boolean |
| `WhenGreaterThan`          | Determines if ValueA is greater than ValueB                | Numeric, Date                  |
| `WhenGreaterThanOrEqualTo` | Determines if ValueA is greater than or equal to ValueB    | Numeric, Date                  |
| `WhenLessThan`             | Determines if ValueA is less than ValueB                   | Numeric, Date                  |
| `WhenLessThanOrEqualTo`    | Determines if ValueA is less than or equal to ValueB       | Numeric, Date                  |
| `WhenNotContains`          | Determines if ValueA is not contained within ValueB        | String                         |
| `WhenNotEqualTo`           | Determines if ValueA is not equal to ValueB                | String, Numeric, Date, Boolean |
| `WhenNotWithinArray`       | Determines if ValueA is not within the CSV array of ValueB | String, Numeric, Date          |
| `When`                     | Determines if ValueA is equal to ValueB                    | String, Numeric, Date, Boolean |
| `WhenNot`                  | Determines if ValueA is not equal to ValueB                | String, Numeric, Date, Boolean |
| `WhenWithinArray`          | Determines if ValueA is within the CSV array of ValueB     | String                         |

---

### Condition Examples

#### WhenContains

The **WhenContains** condition is used to test for the presence of one string within another.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenContains",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenEqualTo

The **WhenEqualTo** condition is used to positively test the equality of two values.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenEqualTo",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenGreaterThan

The **WhenGreaterThan** condition is used to determine if one value is greater than another.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenGreaterThan",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenGreaterThanOrEqualTo

The **WhenGreaterThanOrEqualTo** condition determines if one value is greater than or equal to another.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenGreaterThanEqualTo",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenLessThan

The **WhenLessThan** condition determines if one value is less than another.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenLessThan",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenLessThanOrEqualTo

The **WhenLessThanOrEqualTo** condition is used to determine if one value is less than or equal to another.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenLessThanOrEqualTo",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```

#### WhenNotContains

The **WhenNotContains** condition determines if one string value is not present within another.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenNotContains",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenNotEqualTo

The **WhenNotEqualTo** condition is used to negatively test the equality of two values.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenNotEqualTo",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenNotWithinArray

The **WhenNotWithinArray** condition is used to test if one value does not matches any of the values defined in the comma separated array.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenNotWithinArray",
        "@ValueA": "Item1,Item2",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### When

The **When** condition is used to positively test the equality of two values.

> Note: this condition has identical behaviour to **WhenEqualTo**

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "When",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenNot

The **WhenNot** condition is used to negatively test the equality of two values.

> Note: this condition has identical behaviour to **WhenNotEqualTo**

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenNot",
        "@ValueA": "ValueA",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

#### WhenWithinArray

The **WhenWithinArray** condition is used to test if one value matches any of the values defined in the comma separated array.

--CodeTab--

```json
"Condition": [
    {
        "@xsi:type": "WhenWithinArray",
        "@ValueA": "Item1,Item2",
        "@ValueB": "ValueB"
    }
]
```
--/CodeTab--

### Condition Timing and Evaluation Summary

Conditions control **whether or not a group is processed**, and they are evaluated **before all other group logic**.

---

#### Evaluation Order Recap

RQL groups are evaluated in the following strict sequence:

1. **Conditions** – Must be satisfied before the group runs
2. **Selector** – Entity path is evaluated only if condition passes
3. **Predicate** – Further narrows fetched data
4. **Filters** – Applies post-fetch filtering logic
5. **Ordering** – Sorts matched entities
6. **Outputs** – Aggregate outputs run first, then scalar values

> **If any `<Condition>` fails**, the entire group and all its children are skipped.

---

#### What Is Skipped

When a group fails its condition:
- The `@Selector`, `@Predicate`, `Filters`, and `Outputs` are **not processed**
- **No variables** are evaluated or set
- **All nested groups** are skipped recursively

---

#### No `ElseGroup` Support

There is no formal concept of an `else` branch in RQL.

If alternative logic is needed (e.g., render X when true, render Y when false), you must create **two sibling groups** using opposite conditions (`When` vs. `WhenNot`) or mutually exclusive variable values.



---

Use conditions to cleanly separate query logic, eliminate unneeded data processing, and control which portions of the query structure are included at runtime.

## Filters  
  
Filters allow you to selectively include resources in the matched set. When a group includes filters, only entities with property values that match the filter conditions will be processed.  

The filters are defined within an entity group and are applied against the matched entity set. Only entities that match the filter specifications are included in the query output.  

There are many types of filter as briefly described in the following table:  

| Filter Name        | Description                                                                     | Supported Data Types           |
|--------------------|---------------------------------------------------------------------------------|--------------------------------|
| ActiveOn           | Match entities (having start and end dates) active on specified date.           | Date                           |
| ActiveWithin       | Match entities (having start and end dates) active between the specified dates. | Date                           |
| Between            | Match date properties that fall between the specified dates.                    | Date                           |
| Contain            | Match properties that contain the specified substring.                          | String                         |
| EndsWith           | Match properties that end with the specified substring.                         | String                         |
| EqualTo            | Match properties that exactly equal the specified value                         | String, Numeric, Date, Boolean |
| GreaterThan        | Match properties greater than the specified value                               | Numeric, Date                  |
| GreaterThanEqualTo | Match properties equal to or greater than the specified value                   | Numeric, Date                  |
| HasFlag            | Match properties having a bitwise match to the specified value                  | (PayRunIO Flags Enumerations)  |
| IsNotNull          | Match any non-null property value                                               | String, Numeric, Date, Boolean |
| IsNull             | Match any null property value                                                   | String, Numeric, Date, Boolean |
| LessThan           | Match property values less than the specified value                             | Numeric, Date                  |
| LessThanEqualTo    | Match property values equal to or less than the specified value                 | Numeric, Date                  |
| NotContain         | Match properties that do not contain the specified substring.                   | String                         |
| NotEqualTo         | Match properties not equal to the specified value                               | String, Numeric, Date, Boolean |
| NotOfType          | Only match entities not of the specified data types                             | N/A                            |
| NotWithinArray     | Match properties not present within the specified value array                   | String, Numeric, Date          |
| OfType             | Only match entities of the specified data types                                 | N/A                            |
| StartsWith         | Match properties that begin with the specified substring                        | String                         |
| TakeFirst          | Filter to the number of first matched entities as indicated                     | N/A                            |
| WithinArray        | Match properties present within the specified value array                       | String, Numeric, Date          |

:::info
“Take first” is a special filter often used in conjunction with “Ordering” to select the first ‘n’ number of items from the matched set.
:::

:::success
“Of Type” and "NotOfType" are unique within the filter family. The filter is applied against the entity data type when dealing with pluralised type endpoints.  
An example of multi type endpoint would be the employee Pay Instructions.  
:::

#### Filter Example 1
  
The following example demonstrates filtering matched pay runs to those having a payment date of 31st May 2025.  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "ER001"
        },
        {
          "@Name": "[PayScheduleKey]",
          "@Value": "SCH001"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "PayRuns",
          "@ItemName": "PayRun",
          "@Selector": "/Employer/[EmployerKey]/PaySchedule/[PayScheduleKey]/PayRuns",
          "Filter": [
            {
              "@xsi:type": "EqualTo",
              "@Property": "PaymentDate",
              "@Value": "2025-05-31"
            }
          ]
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Filter Example 2
  
The below example filters pay lines by tax year and two possible tax periods.  

:::info
**'IsOr' filters**  
The below RQL example includes filter using the **IsOr** specification. The match is considered true when any **IsOr** filter matches.  
When using **IsOr** all possible filter matches must indicate _OR_.  
:::
  
--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "ER001"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "PayRuns",
          "@ItemName": "PayRun",
          "@Selector": "/Employer/[EmployerKey]/Employee/*/PayLines",
          "Filter": [
            {
              "@xsi:type": "EqualTo",
              "@Property": "TaxYear",
              "@Value": "2025"
            },
            {
              "@xsi:type": "EqualTo",
              "@Property": "TaxPeriod",
              "@Value": "1",
              "@IsOr": "true"
            },
            {
              "@xsi:type": "EqualTo",
              "@Property": "TaxPeriod",
              "@Value": "2",
              "@IsOr": "true"
            }
          ],
          "Output": {
            "@xsi:type": "Sum",
            "@Name": "TotalNetPay",
            "@Property": "Value"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

### Filter Type Examples  

The following sub-sections describe all the available types of filters with an example in code.  

#### ActiveOn

Matches all entities that have a start and end date spanning the specified date. An example of a **start end** entity would be the _PayInstruction_.  

--CodeTabs--

```json
"Filter": [
  {
    "@xsi:type": "ActiveOn",
    "@Value": "2025-05-08"
  }
]
```
--/CodeTabs--

#### ActiveWithin

Matches all entities that have a start and end date that intersects the specified date range.  

:::warning
**Two dates, one value**  
This filter uses a special comma-separated pair of dates to define the matching range.  
:::

--CodeTabs--

```json
"Filter": [
  {
    "@xsi:type": "ActiveWithin",
    "@Value": "2024-04-06,2025-04-05"
  }
]
```
--/CodeTabs--

#### Between

Matches all entities that have a date property within specified date range. This also matches dates equal to the start and end date.  

--CodeTabs--

```json
"Filter": [
  {
    "@xsi:type": "Between",
    "@Property": "StartDate",
    "@Value": "2025-05-01",
    "@Value2": "2025-05-31"
  }
]
```
--/CodeTabs--

#### Contain

Matches all entities that have a property value containing the specified filter value. This filter can be used on properties that contain strings.  

--CodeTabs--

```json
"Filter": [
   {
     "@xsi:type": "Contain",
     "@Property": "Description",
     "@Value": "a string value"
   }
]
```
--/CodeTabs--

#### EndsWith

Matches all entities that have a property value ending with the specified filter value.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "EndsWith",
      "@Property": "Description",
      "@Value": "end match"
    }
]
```
--/CodeTabs--

#### EqualTo

Matches all entities that have a property value matching the specified value.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "EqualTo",
      "@Property": "FirstName",
      "@Value": "Terry"
    }
]
```
--/CodeTabs--

#### GreaterThan

Matches all entities with a property value exceeding the specified value. This filter works with numeric and date properties.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "GreaterThan",
      "@Property": "Value",
      "@Value": "100"
    }
]
```
--/CodeTabs--

#### GreaterThanEqualTo

Matches all entities with a property value equal or exceeding the specified value. This filter works with numeric and date properties.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "GreaterThanEqualTo",
      "@Property": "Value",
      "@Value": "100"
    }
]
```
--/CodeTabs--

#### HasFlag

Matches all entities having a match to the specified PayRunIO Flag Enum value.  

An example of a PayRunIO flag enumeration would be the working week.  

```csharp
namespace PayRunIO.v2.Core.Enums
{
    using System;

    [Flags]
    public enum WorkingWeek
    {
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16,
        Saturday = 32,
        Sunday = 64,
        AllWeekDays = Monday | Tuesday | Wednesday | Thursday | Friday,
        AllDays = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday
    }
}
```

The filter uses the name string (of the enumeration value) to determine the match as demonstrated in the following table.  

| Property Value | Filter Value | Is Matched |
|----------------|--------------|------------|
| Monday Tuesday | Wednesday    | ❌         | 
| AllWeekDays    | Monday       | ✅         | 
| AllWeekDays    | Sunday       | ❌         | 
| AllDays        | Friday       | ✅         | 
  
--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "HasFlag",
      "@Property": "WorkingWeek",
      "@Value": "Monday"
    }
]
```
--/CodeTabs--

#### IsNotNull

Matches all entities having a non-null property value.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "IsNotNull",
      "@Property": "LeavingDate"
    }
]
```
--/CodeTabs--

#### IsNull

Matches all entities having a null property value.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "IsNull",
      "@Property": "LeavingDate"
    }
]
```
--/CodeTabs--

#### LessThan

Matches all entities having a property value below the specified filter value. This filter works with numeric and date properties.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "LessThan",
      "@Property": "Value",
      "@Value": "100"
    }
]
```
--/CodeTabs--

#### LessThanEqualTo

Matches all entities having a property value equal or less than the specified filter value. This filter works with numeric and date properties.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "LessThanEqualTo",
      "@Property": "Value",
      "@Value": "100"
    }
]
```
--/CodeTabs--

#### NotContain

Matches all entities that have a property value not containing the specified filter value. This filter can be used on properties that contain strings.  

--CodeTabs--

```json
"Filter": [
   {
     "@xsi:type": "NotContain",
     "@Property": "Description",
     "@Value": "a value"
   }
]
```
--/CodeTabs--

#### NotEqualTo

Matches all entities that have a property value not matching the specified value. The filter can be used with string properties.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "NotEqualTo",
      "@Property": "LastName",
      "@Value": "Jones"
    }
]
```
--/CodeTabs--

#### NotHasFlag

Matches all entities having a bitwise flags property that do not match to the specified PayRunIO Flag Enum value.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "NotHasFlag",
      "@Property": "WorkingWeek",
      "@Value": "Sunday"
    }
]
```
--/CodeTabs--

#### NotOfType  
  
Filters all entities within the matched entity group which do not match the specified entity type. This filter can be used on entity match groups that contain many entity types, for example: Employee Pay Instructions.  

--CodeTabs--

```json
"Filter": [
     {
       "@xsi:type": "NotOfType",
       "@Value": "TaxPayInstruction"
     }
]
```
--/CodeTabs--

#### NotWithinArray  
  
Considers all values in comma-separated filter value, and identifies all properties not having a matching value.  

--CodeTabs--

```json
"Filter": [
     {
       "@xsi:type": "NotWithinArray",
       "@Property": "Description",
       "@Value": "ValueA,ValueB,ValueC"
     }
]
```
--/CodeTabs--

#### OfType  
  
Matches all entities having a type matching the specified filter value.This filter can be used on entity match groups that contain many entity types, for example: Employee Pay Instructions.  

--CodeTabs--

```json
"Filter": [
     {
       "@xsi:type": "OfType",
       "@Value": "PayLineTax"
     }
]
```
--/CodeTabs--

#### StartsWith

Matches all entities that have a property value starting with the specified filter value. This filter can be used with string value properties.  

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "StartsWith",
      "@Property": "Description",
      "@Value": "start value"
    }
]
```
--/CodeTabs--

#### TakeFirst

This special filter can be used to restrict the number of matched entities in an entity group. 

> *Note: The **TakeFirst** filter should be combined with an ordering expression.*   

--CodeTabs--

```json
"Filter": [
    {
      "@xsi:type": "TakeFirst",
      "@Value": "10"
    }
]
```
--/CodeTabs--

#### WithinArray  
  
Considers all values in comma-separated filter value and matches all properties having a matching value.  

--CodeTabs--

```json
"Filter": [
     {
       "@xsi:type": "WithinArray",
       "@Property": "MiddleName",
       "@Value": "John,Simon,Zack"
     }
]
```
--/CodeTabs--

## Predicate vs Filter vs Condition

In RQL, **Predicates**, **Filters**, and **Conditions** are all tools used to control query logic, but they differ in *where*, *when*, and *how* they act during query execution.

### Summary Comparison

| Feature       | Purpose                                                  | Used In                        | Evaluated When            | Affects                     | Supports Variables | When to Use                                |
|-------------- |----------------------------------------------------------|-----------------------------   |---------------------------|-----------------------------|------------------- |-------------------------------------------  |
| **Predicate** | Restrict **database query scope** for child groups       | `@Predicate` on `<Group>`      | **Before** database fetch | Which child data is fetched | ✅ Yes             | When data scope is **well-known**          |
| **Filter**    | Refine **already-fetched** entities in the group         | `<Filter>` elements            | **After** database fetch  | Which entities are included | ✅ Yes             | When reusing group or **iterating deeply** |
| **Condition** | Include or exclude a **group block** entirely            | `<Condition>` inside `<Group>` | Before group processing   | Whether the group runs      | ✅ Yes             | When toggling **group existence**          |

### In Detail

#### Predicate

- **Acts as a pre-fetch filter**—it restricts the scope of what’s loaded from the database.
- Specified as an attribute directly on the group element.
- Syntax example:
  
- **Best used** when:
  - The **scope of matching data is narrow and well-known**.
  - You want to avoid loading unnecessary records.
- **More performant** when filtering large datasets.

#### Filter

- **Acts as a post-fetch filter**—applied after the group’s entity collection has been retrieved.
- Defined within `<Filter>` child elements.
- Syntax example:
  
- **Best used** when:
  - You want to refine a **wider initial result set**.
  - You may **revisit the group** multiple times in nested groups.
- Offers **fine-grained control** but does not limit the initial data retrieval.

#### Condition

- **Controls the execution of a group itself**—like a runtime `if` statement.
- Used for **entire group enablement or suppression**.
- Syntax example:
  
- **Best used** when:
  - Certain groups should only be included based on **user input or context variables**.

### Side-by-Side Example







## Ordering  
  
This aspect of the query allows to sort the results of a matching group by property values.  
  
Ordering can be either:  
  
* Ascending
* Descending  

### Ascending  

--CodeTabs--

```json
"Order": [
          {
            "@xsi:type": "Ascending",
            "@Property": "LastName"
          }
        ]
```
--/CodeTabs--

### Descending  

--CodeTabs--

```json
"Order": [
          {
            "@xsi:type": "Descending",
            "@Property": "StartDate"
          }
        ]
```
--/CodeTabs--

### Example Including Ordering 
  
--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "ER001"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "Employees",
          "@ItemName": "Employee",
          "@Selector": "/Employer/[EmployerKey]/Employees",
          "@UniqueKeyVariable": "[EmployeeKey]",
          "Output": [
            {
              "@xsi:type": "RenderProperty",
              "@Name": "FirstName",
              "@Property": "FirstName"
            },
            {
              "@xsi:type": "RenderProperty",
              "@Name": "LastName",
              "@Property": "LastName"
            }
          ],
          "Order": [
            {
              "@xsi:type": "Ascending",
              "@Property": "LastName"
            },
            {
              "@xsi:type": "Ascending",
              "@Property": "FirstName"
            }
          ]
        }
      ]
    }
  }
}
```
--/CodeTabs--

## Loop Expressions  
  
In special circumstances, you may need to repeat the matching group while setting an incrementing variable. Loop expressions allow for this behaviour.  
Loop expressions are represented as a single string, normally formed with an identifying prefix followed by a range specification.  
During the loop, the “[LoopVariable]” variable is updated, allowing the loop values to be used in the in-scope group processing.  
  
An example of looping could be to repeat a group for each tax period of the year.  
  
Supported loop expressions are:  
  
* All pay schedule periods  
  
Expression: AllPaySchedulePeriods
  
* All tax months  
  
Expression: AllTaxMonths  
  
Note: Requires that “[TaxYear]” variable has been set.  
  
* Comma-separated list of values  
  
Expression: CSV:valueA,valueB,etc  
  
* Range of numbers  
  
Expression: Range:1-100

**Example Query With Loop Expressions**  
  
--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "LoopExpressionExample",
    "SuppressMetricAttributes": "true",
    "Groups": {
      "Group": [
        {
          "@GroupName": "CsvLoopExpression",
          "@LoopExpression": "CSV:2022-01-01,2022-02-01,2022-03-01",
          "Output": {
            "@xsi:type": "RenderValue",
            "@Name": "Value",
            "@Value": "[LoopVariable]"
          }
        },
        {
          "@GroupName": "RangeLoopExpression",
          "@LoopExpression": "Range:100-102",
          "Output": {
            "@xsi:type": "RenderValue",
            "@Name": "Value",
            "@Value": "[LoopVariable]"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

**Results**  
  
--CodeTabs--

```json
{
  "LoopExpressionExample": {
    "CsvLoopExpression": {
      "Value": [
        "2022-01-01",
        "2022-02-01",
        "2022-03-01"
      ]
    },
    "RangeLoopExpression": {
      "Value": [
        "100",
        "101",
        "102"
      ]
    }
  }
}
```
--/CodeTabs--

## Data Types and Formatting

RQL queries interact with a variety of entity properties and output values. These can represent strings, numbers, dates, booleans, and other simple data types. This section explains the expected data types, supported formatting options, and where each can be applied.

---

### Supported Data Types

| Type      | Description                                                  | Common Uses                      |
|-----------|--------------------------------------------------------------|----------------------------------|
| `String`  | Plain text values                                            | Names, codes, labels             |
| `Numeric` | Decimal or whole number values                               | Totals, monetary amounts         |
| `DateTime`| ISO 8601-compliant date values (e.g. `2025-05-31`)          | Payment dates, birth dates       |
| `Boolean` | `true` or `false`                                           | Flags, switches, options         |

---

### Format Support

Formatting is most commonly used with **Output types** that display or compute values. The following output types support formatting:

- `RenderValue`
- `RenderProperty`
- `RenderTaxPeriodDate`
- `RenderTaxPeriod`
- `RenderDateAdd`
- `ExpressionCalculator`
- Aggregates (`Sum`, `Avg`, `Min`, `Max`, etc.)

---

### Date Format Examples

RQL uses standard .NET-compatible formatting strings for date types.

| Format String     | Result Example        | Notes                        |
|-------------------|------------------------|------------------------------|
| `yyyy-MM-dd`      | `2025-05-31`           | ISO-like sortable date       |
| `dd/MM/yyyy`      | `31/05/2025`           | Common UK short date         |
| `MMMM yyyy`       | `May 2025`             | Full month name              |
| `ddd, dd MMM yyyy`| `Sat, 31 May 2025`     | Friendly long-form           |

---

### Number Format Examples

| Format String | Result Example | Notes                          |
|---------------|----------------|--------------------------------|
| `0.00`        | `1234.56`      | Fixed 2 decimal places         |
| `#,##0.00`    | `1,234.56`     | Thousands separator            |
| `0`           | `1235`         | Rounded integer (no decimals)  |
| `0.0000`      | `1234.5678`    | 4 decimal precision            |

---

### Example: Formatting with RenderValue

--CodeTabs==

```json
"Output": 
[
  {
    "@xsi:type": "RenderValue",
    "@Name": "PayDate",
    "@Value": "[PaymentDate]",
    "@Format": "dd/MM/yyyy"
  },
  {
    "@xsi:type": "RenderValue",
    "@Name": "NetPay",
    "@Value": "[NetPay]",
    "@Format": "0.00"
  }
]
```
--/CodeTabs--

---

### Handling Nulls and Defaults

- If a property or variable value is `null`, formatting is skipped and an empty element or attribute is rendered.
- Default values are not automatically applied. You must explicitly use a fallback value, or a conditional group to handle this.

---

This formatting control helps you present query outputs in a user-friendly and consistent manner across XML and JSON response types.

## Output Evaluation Context

Understanding the timing and evaluation flow of outputs is essential for writing effective and predictable RQL queries. This section describes how outputs are processed in relation to entity matching, filters, conditions, and nested groups.

---

### Output Lifecycle Overview

The evaluation order within a group follows this sequence:

1. **Conditions** – Determines if the group should be processed at all.
2. **Entity Selection** – Performed via `@Selector` and `@Predicate`.
3. **Filtering** – Filters reduce the matched set of entities.
4. **Ordering** – Orders the matched entities based on `OrderBy` elements.
5. **Aggregate Outputs** – Aggregations (`Sum`, `Avg`, `Count`, etc.) are evaluated **first**.
6. **Scalar Outputs** – Individual `Render*`, `RenderValue`, `RenderProperty`, etc., are evaluated **in declaration order**.

---

### Variable Timing and Scope

- Variables are **globally scoped** and persist through the entire query.
- They are updated **sequentially** as the query traverses down the group hierarchy.
- Variables written in one group are immediately available to any child or sibling groups processed afterward.

> **Note**: Variables from skipped groups (due to `<Condition>`) are **not set**.

---

### Skipped Groups

- Groups excluded by `<Condition>` statements are **never evaluated**.
- Outputs and variable assignments within such groups are skipped entirely.
- Ensure conditional values are calculated **before** the group that depends on them.

---

### Empty Match Behavior

- If a group’s `Selector` returns **zero matches**, the group and its child groups are **not processed**.
- As a result, no outputs or variables are created from such groups.
- Groups **without a selector** are always processed and can be used to generate headers, totals, or summary blocks.

---

### Output Types and Evaluation Timing

- **All outputs** share the same evaluation logic: the value is calculated first, then routed to its target (element, attribute, variable, etc.).
- Output **targets** determine the result destination:
  - `Element`, `Attribute`, `InnerText` → Render to XML/JSON response
  - `Variable`, `VariableSum`, `VariableAppend`, `VariablePrepend` → Write to variable

> Outputs are evaluated **in declaration order**, except **aggregates**, which are always processed **before scalar outputs**.

---

### Nested Group Evaluation

- Groups are evaluated **depth-first** in a single-threaded, top-down order.
- A parent group is fully processed **before** any of its children.
- Outputs within the parent are evaluated before the execution proceeds to nested `<Group>` nodes.

---

### Loop Expression Behavior

- Groups using `LoopExpression` evaluate once **per loop value**.
- For each iteration:
  - The `[LoopVariable]` is updated.
  - The group is re-evaluated with outputs and filters applied anew.
- Any output to a global variable will **overwrite or append** across iterations depending on the `Output` type.

---

### Example: Evaluation Flow

--CodeTabs--

```json
"Group": [
  {
    "@Selector": "/Employers",
    "@UniqueKeyVariable": "[EmployerKey]",
    "Output": {
      "@xsi:type": "RenderValue",
      "@Output": "Variable",
      "@Name": "[EmployerName]",
      "@Value": "[Name]"
    },
    "Group": {
      "@Selector": "/Employer/[EmployerKey]/Employees",
      "Output": [
        {
          "@xsi:type": "Count",
          "@Name": "EmployeeCount"
        },
        {
          "@xsi:type": "RenderValue",
          "@Name": "Label",
          "@Value": "[EmployerName] has employees."
        }
      ]
    }
  }
]
```
--/CodeTabs--

- The **outer group** fetches employers and assigns `[EmployerKey]` and `[EmployerName]`
- The **inner group** counts employees for that employer and uses the inherited `[EmployerName]` in its output

---

This evaluation model helps ensure predictable, scoped, and reusable query logic—essential.

## Advanced Features Pt.1
  
Released as part of the 2022/23 tax year updates, we have extended the RQL feature set with some new quality of life and performance features.  
  
### Filtering With Or  
  
The filtering aspect of RQL was previously limited to a "must match all" list where the selected entities (within a group) must match all filter expressions to be included.  

We have now extended the filter behaviour to allow for an optional "must match at least one" list.  
  
Using the new *IsOr* filter option, it is now possible to specify a collection of filter conditions that the entity must match at least one of.  

**Filtering with OR example**
  
This filter example selects all employees having a first name of either Rod, Jane or Freddie  
  
--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OrFilterExample",
    "Groups": {
      "Group": {
        "@GroupName": "Employees",
        "@ItemName": "Employee",
        "@Selector": "/Employer/*/Employees",
        "Filter": [
          {
            "@xsi:type": "EqualTo",
            "@Property": "FirstName",
            "@Value": "Rod",
            "@IsOr": "true"
          },
          {
            "@xsi:type": "EqualTo",
            "@Property": "FirstName",
            "@Value": "Jane",
            "@IsOr": "true"
          },
          {
            "@xsi:type": "EqualTo",
            "@Property": "FirstName",
            "@Value": "Freddie",
            "@IsOr": "true"
          }
        ],
        "Output": {
          "@xsi:type": "RenderLink"
        }
      }
    }
  }
}
```
--/CodeTabs--

### Wildcard URL support  
  
Sometimes walking the hierarchy of API entities can lead to query performance issues. Consider the following scenario:
  
You need to create a management overview query that lists all pay runs requiring an FPS transmission. This can be accomplished by checking for the existence of the *FpsPending* tag on a pay run entity.  
  
Using the pre-2022 RQL, you would need to iterate all employers, all pay schedules and all pay runs in order to check for the existence of the *FpsPending* tag.  
  
**Fps Pending Old Example**

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "FpsPendingExample",
    "SuppressMetricAttributes": "true",
    "Groups": {
      "Group": {
        "@Selector": "/Employers",
        "@UniqueKeyVariable": "[EmployerKey]",
        "Group": {
          "@Selector": "/Employer/[EmployerKey]/PaySchedules",
          "@UniqueKeyVariable": "[PayScheduleKey]",
          "Group": {
            "@Selector": "/Employer/[EmployerKey]/PaySchedule/[PayScheduleKey]/PayRuns/Tag/FpsPending",
            "Output": {
              "@xsi:type": "RenderLink"
            }
          }
        }
      }
    }
  }
}
```
--/CodeTabs--
  
This process is very time consuming because each entity must be loaded in order to find the child items. Load all employers, load all pay schedules for that employer, load each pay run for that pay schedule, check for *FpsPending* tag...  

To help relieve this incremental problem (sometimes referred to as the n+1 problem), we have introduced URL wildcard support.  
  
Using a wildcard character (*) in place of the unique key enables fewer database queries.  

**Fps Pending wildcard Example**

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "FpsPendingExample",
    "SuppressMetricAttributes": "true",
    "Groups": {
        "Group": {
          "@Selector": "/Employer/*/PaySchedule/*/PayRuns/Tag/FpsPending",
          "Output": {
            "@xsi:type": "RenderLink"
          }
        }
      }
    }
  }
}
```
--/CodeTabs--

:::success
**Please Note**  
Wildcard characters can only be used to replace entity unique keys on URLs that match a collection of entities.  
  
**Invalid**: /Employer/ER001/Employee/*  
**Valid**: /Employer/ER001/Employee/*/PayLines  
:::

### Direct DB Predicates  
  
Direct DB predicates are a new group option which allow you to specify filters that are directly applied at the database query level.  
  
RQL filters are applied against the loaded API entities, so in order to find all items that match the specified filter, the query processor must first load that entity from the database and then compare it's properties to the filter specification.
Entities that do not match are then excluded from the output processing.  
  
Unfortunately when dealing with larger data sets, this can result in a wasteful process where a great many entities are loaded only to then be ignored.  Loading entities takes time and results in slower query performance.  
  
To help avoid this issue, you can apply direct DB predicates on the RQL group object.  
  
The Group RQL object now has a *Predicate* property which allows you include a direct DB predicate to match a subset of entities.  

| Reductive Process | When Applied                             | Benefits                                                         | Liabilities                                                                                          |
|-------------------|------------------------------------------|------------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| Filters           | After entities are loaded from database  | Allows for result set caching avoiding duplicated data retrieval | Loads all data set entites before discarding unwanted                                                |
| Predicates        | Before entities are loaded from database | Reduces number of items loaded into result set                   | Not cached for reuse, requires specialised knowledge of entity internal properties and relationships |

**Filtering Example**  
  
> *Note: Using filters, all pay runs are loaded into memory and then compared against the filters*  
  
--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "PredicateExample",
    "SuppressMetricAttributes": "true",
    "Variables": {
      "Variable": [
        {
          "@Name": "[StartDate]",
          "@Value": "2022-01-01"
        },
        {
          "@Name": "[EndDate]",
          "@Value": "2022-01-31"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "FilterExample",
          "@Selector": "/Employer/*/PaySchedule/*/PayRun",
          "Filter": [
            {
              "@xsi:type": "GreaterThanEqualTo",
              "@Property": "PaymentDate",
              "@Value": "[StartDate]"
            },
            {
              "@xsi:type": "LessThanEqualTo",
              "@Property": "PaymentDate",
              "@Value": "[EndDate]"
            }
          ],
          "Output": {
            "@xsi:type": "RenderLink"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

**Predicate Example**  
  
> *Note: Using a predicate, only pay runs that match the predicate expression are loaded from the database*  
  
--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "PredicateExample",
    "SuppressMetricAttributes": "true",
    "Variables": {
      "Variable": [
        {
          "@Name": "[StartDate]",
          "@Value": "2022-01-01"
        },
        {
          "@Name": "[EndDate]",
          "@Value": "2022-01-31"
        }
      ]
    },
    "Groups": {
      "Group": 
        {
          "@Selector": "/Employer/*/PaySchedule/*/PayRun",
          "@Predicate": "PaymentDate >= [StartDate] AND PaymentDate <= [EndDate]",
          "Output": {
            "@xsi:type": "RenderLink"
          }
        }
      }      
    }
  }
}
```
--/CodeTabs--

:::danger
**WARNING - Use direct DB predicates with caution.**  
  
Each iteration of a group that includes a predicate will result in a database query.  
  
If the group is nested and needs to be iterated multiple times, you should use filters.  
  
When using filters, the database results are cached. When using predicates, the DB is queried on every iteration!
:::

#### Predicate Patterns  

Direct DB predicates are specified in the following pattern:

* <span style="color:#0033cc">ColumnName</span> <span style="color:#33cc33">operator</span> <span style="color:#ff6600">operand</span> (optional)<span style="color:#cc33ff">AND</span>/<span style="color:#cc33ff">OR</span>
  
Multiple predicates can be joined using either AND or OR:
  
* <span style="color:#0033cc">PaymentDate</span> <span style="color:#33cc33">=</span> <span style="color:#ff6600">'2022-01-01'</span> <span style="color:#cc33ff">OR</span> <span style="color:#0033cc">PaymentDate</span> <span style="color:#33cc33">=</span> <span style="color:#ff6600">'2022-02-01'</span>
* <span style="color:#0033cc">PaymentDate</span> <span style="color:#33cc33">>=</span> <span style="color:#ff6600">'2022-01-01'</span> <span style="color:#cc33ff">AND</span> <span style="color:#0033cc">PaymentDate</span> <span style="color:#33cc33"><=</span> <span style="color:#ff6600">'2022-01-31'</span>

When using *OR* the left and right statements adjacent to the *OR* are conjoined into a true false test.  
  
* <span style="color:#0033cc">PayFrequency</span> <span style="color:#33cc33">=</span> <span style="color:#ff6600">'Weekly'</span> <span style="color:#cc33ff">AND</span> <span style="color:#0033cc">PaymentDate</span> <span style="color:#33cc33">=</span> <span style="color:#ff6600">'2022-02-01'</span> <span style="color:#cc33ff">OR</span> <span style="color:#0033cc">PaymentDate</span> <span style="color:#33cc33">=</span> <span style="color:#ff6600">'2022-02-02'</span>
* The pay frequency must be weekly and the payment date must be either 1st or 2nd of February  

#### Operators  
  
The following list details the available operators:  
  
* = : Equal to
* != : Not equal to
* \> : Greater than
* \>= : Greater than or equal to
* < : Less than
* <= : Less than or equal to

#### Special operators
  
* CONTAINS : can be used on string data types to determine if the specified substring is contained within the target.
* EXCLUDES : can be used on string data types to determine if the specified substring is not contained within the target.
* OFTYPE : Allows for the specification of returned data types. Used when a single endpoint returns multiple data type entities, i.e. PayLines.

#### CONTAINS Example  
  
* **Description CONTAINS 'Bonus'**
* Match all entities having a Description value containing the phrase 'Bonus'

#### EXCLUDES Example  
  
* **Description EXCLUDES 'Commission'**
* Match all entities having a Description value that excludes the phrase 'Commission'

#### OFTYPE Example  
  
> The **OFTYPE** operator is specified slightly differently than standard. It is used in place of the normal property specification and pair with an equality operator.  

* **OFTYPE = 'PayLineTax'**
* Match all pay lines of type PayLineTax
  
#### Data types
  
The following list details the supported predicate data types.  
  
* Integer : 123
* Decimal : 0.99
* Date : '2022-01-01'
* DateTime : '2022-01-01T18:00:00'
* String : 'A String'
* Null : null
* PayRun Enums : 'Weekly'

### Regular Expression Support  
  
The final helpful update to RQL in the 2022/23 tax year release is regular expression support.  
  
The *RenderValue* RQL output object now allows the specification of a regular expression to be applied to the rendered output.  

**Example Query**  
  
--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "RegExExample",
    "SuppressMetricAttributes": "true",
    "Groups": {
      "Group": {
        "@GroupName": "PayRuns",
        "@ItemName": "PayRun",
        "@Selector": "/Employer/*/PaySchedule/*/PayRuns",
        "@UniqueKeyVariable": "[PayRunKey]",
        "Output": [
          {
            "@xsi:type": "RenderProperty",
            "@Output": "Variable",
            "@Name": "[PayScheduleUrl]",
            "@Property": "PaySchedule.Href"
          },
          {
            "@xsi:type": "RenderValue",
            "@Name": "PayRunKey",
            "@Value": "[PayRunKey]"
          },
          {
            "@xsi:type": "RenderValue",
            "@Name": "PayScheduleKey",
            "@Value": "[PayScheduleUrl]",
            "@Regex": "(?<=\\/PaySchedule\\/)[^\\/]+"
          },
          {
            "@xsi:type": "RenderValue",
            "@Name": "EmployerKey",
            "@Value": "[PayScheduleUrl]",
            "@Regex": "(?<=\\/Employer\\/)[^\\/]+"
          }
        ]
      }
    }
  }
}
```
--/CodeTabs--  
  
**Example Response**
  
--CodeTabs--

```json
{
  "RegExExample": {
    "PayRuns": {
      "PayRun": [
        {
          "PayRunKey": "PR001",
          "PayScheduleKey": "SCH001",
          "EmployerKey": "ER001"
        },
        {
          "PayRunKey": "PR002",
          "PayScheduleKey": "SCH001",
          "EmployerKey": "ER001"
        },
        {
          "PayRunKey": "PR003",
          "PayScheduleKey": "SCH001",
          "EmployerKey": "ER001"
        }
      ]
    }
  }
}
```
--/CodeTabs--

## Advanced Features Pt.2

The 2025/26 tax year introduces an overhaul of the internal RQL (Report Query Language) processing engine, designed to improve performance and throughput.

### Optimised Query Groups

A new `Optimise` option is available for query groups starting April 2025. This Boolean setting (`true` or `false`) can be defined on a query group to control how the underlying data is retrieved from the database.

:::warning
**Only Applies to Collection Endpoints**  
The optimisation process only applies when resolving a *collection* of entities. It is not applied to direct queries targeting a single entity.
:::

### How Optimisation Works

When you enable the `Optimise` option on a query group, the RQL engine will streamline the database retrieval process to return only the data explicitly requested.

Under normal conditions (without optimisation), the engine loads all data needed to populate the full entity object.  
For example, when querying an employee entity, all of the following may be loaded:

- Direct properties (e.g., `FirstName`, `LastName`, etc.)
- Nested child entities (e.g., `Address`, `MetaData`, `BankAccount`, `CostSplits`, etc.)

This can lead to unnecessary data retrieval — and slower queries — especially when only a subset of properties is needed in the output.

:::info
**Optimise Only Loads What You Need**  
Using the `Optimise` option ensures that only the properties used in `Output`, filters, or ordering are retrieved, improving query performance.
:::
  
**Example**  
  
--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "OptimiseExample",
    "Groups": {
      "Group": {
        "@GroupName": "Employees",
        "@Selector": "/Employer/ER001/Employees",
        "@Optimise": "true",
        "Output": [
          {
            "@xsi:type": "RenderProperty",
            "@Name": "FirstName",
            "@Property": "FirstName"
          },
          {
            "@xsi:type": "RenderProperty",
            "@Name": "LastName",
            "@Property": "LastName"
          }
        ]
      }
    }
  }
}
```
--/CodeTabs--

Optimisation is especially beneficial when working with complex or large collections, such as:

- Employers
- Employees
- Sub-contractors
- Pension Schemes

### What's Included and Excluded

| Feature                | Without Optimise | With Optimise                           |
|------------------------|------------------|-----------------------------------------|
| Full entity loading    | ✅               | ❌                                     |
| Direct properties      | ✅               | ✅ (if listed in outputs/filters)      |
| Nested child entities  | ✅               | ❌ (unless explicitly selected)        |
| Filters & sorting      | ✅               | ✅ (still applied)                     |
| `RenderEntity` output  | ✅               | ❌ Not supported                       |

---

### Optimisation Limitations

Optimisation is a powerful advanced feature, but it comes with the following limitations:

- **Collection Endpoints Only**  
  Optimisation only applies to query groups targeting *collection* endpoints (e.g. `/Employer/ER001/Employees`).  
  It is **not applied** to direct single-entity queries (e.g. `/Employer/ER001/Employee/EE001`).

- **Incompatible with `RenderEntity` Output**  
  Optimisation cannot be used with output definitions that render the entire entity using the `RenderEntity` type.

- **Limited Support for Plural Type Endpoints**  
  Optimisation has limited compatibility with API endpoints that return multiple data types.  
  When optimising such endpoints, only the properties shared by all types (i.e., defined on their *common ancestor*) will be available.

:::danger
**Plural Type Endpoints**  
For multi-type endpoints, only properties defined on the shared base type (common ancestor) are accessible when optimised.
:::

#### Example: Plural Type Endpoint

Consider a scenario where employees have multiple types of pay instructions:

- `PrimitiveInstruction`
- `RateInstruction`
- `TaxInstruction`
- `NIInstruction`

All of these inherit from a common base type: **PayInstruction**.

When accessing an employee’s pay instructions using an optimised group, **only the properties defined on `PayInstruction`** will be available.  
Any subtype-specific fields will be excluded.

## Advanced Techniques

### Use Variable Assignment to Control Output

Sometimes you need fine-tuned control over the structure of your query output. This often occurs when the desired output requires values to appear in a specific column order.

#### Example: Query With Incorrect Output Order

Consider the following query that generates a report with two main sections:

1. A "Headers" section that outputs column headers "Employer Name", "First Name", and "Last Name".
2. A "Rows" section that lists each employee under the employer with key "ER001". For each employee, it outputs their first and last names and includes the employer's name.

However, this query has a problem—the "Employer Name" value appears in the third column instead of the first.

--CodeTabs--


```json
{
  "Query": {
    "RootNodeName": "ReportRoot",
    "Variables": {
      "Variable": [
        { "@Name": "[EmployerKey]", "@Value": "ER001" }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "Headers",
          "Output": [
            { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "Employer Name" },
            { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "First Name" },
            { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "Last Name" }
          ]
        },
        {
          "@GroupName": "Rows",
          "@ItemName": "Row",
          "@Selector": "/Employer/[EmployerKey]/Employees",
          "Output": [
            { "@xsi:type": "RenderProperty", "@Name": "col", "@Property": "FirstName" },
            { "@xsi:type": "RenderProperty", "@Name": "col", "@Property": "LastName" }
          ],
          "Group": {
            "@Selector": "/Employer/[EmployerKey]",
            "Output": { "@xsi:type": "RenderProperty", "@Name": "col", "@Property": "Name" }
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Example Output of Incorrect Query

--CodeTabs--


```json
{
  "ReportRoot": {
    "Headers": {
      "col": ["Employer Name", "First Name", "Last Name"]
    },
    "Rows": {
      "Row": [
        { "col": ["Terry", "Tester", "Test Employer Co."] },
        { "col": ["Tina", "Tester", "Test Employer Co."] }
      ]
    }
  }
}
```
--/CodeTabs--

#### Example: Query Using Variable Assignment to Correct Output Order

To fix this, you can assign values to intermediate variables (e.g., `[FirstName]`, `[EmployerName]`) during data traversal. Then, use a final nested group to output the values explicitly in the correct sequence, matching the header structure.

--CodeTabs--


```json
{
  "Query": {
    "RootNodeName": "ReportRoot",
    "Variables": {
      "Variable": [
        { "@Name": "[EmployerKey]", "@Value": "ER001" }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "Headers",
          "Output": [
            { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "Employer Name" },
            { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "First Name" },
            { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "Last Name" }
          ]
        },
        {
          "@GroupName": "Rows",
          "@ItemName": "Row",
          "@Selector": "/Employer/[EmployerKey]/Employees",
          "Output": [
            { "@xsi:type": "RenderProperty", "@Output": "Variable", "@Name": "[FirstName]", "@Property": "FirstName" },
            { "@xsi:type": "RenderProperty", "@Output": "Variable", "@Name": "[LastName]", "@Property": "LastName" }
          ],
          "Group": [
            {
              "@Selector": "/Employer/[EmployerKey]",
              "Output": {
                "@xsi:type": "RenderProperty",
                "@Output": "Variable",
                "@Name": "[EmployerName]",
                "@Property": "Name"
              }
            },
            {
              "Output": [
                { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "[EmployerName]" },
                { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "[FirstName]" },
                { "@xsi:type": "RenderValue", "@Name": "col", "@Value": "[LastName]" }
              ]
            }
          ]
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Example Output of Correct Query

--CodeTabs--


```json
{
  "ReportRoot": {
    "Headers": {
      "col": ["Employer Name", "First Name", "Last Name"]
    },
    "Rows": {
      "Row": [
        { "col": ["Test Employer Co.", "Terry", "Tester"] },
        { "col": ["Test Employer Co.", "Tina", "Tester"] }
      ]
    }
  }
}
```
--/CodeTabs--

> ✅ **Key Takeaways**
> - Use `Output="Variable"` to temporarily store values for reuse.
> - Use a final `Group` to control the output column order precisely.
> - Always align output order with header definitions to avoid mismatches.

## Common Payroll Values and Where To Find Them

Payroll reporting requires the use of many common data points. Sometimes the location of these values is not immediately obvious. 
Below is a brief guide to where some common data items can be found and the query techniques used to retrieve them.  

### Additional Employee Properties Not Present on the Base Object 

The employee object holds a large number of employee centric values, but some values are stored in other locations.

#### NI Letter

The employees NI Letter is used to calculated the employees and employers national insurance contribution amounts. The letter can be found in the employee summary entity.

> Note: The employee summary is a descendant of the main employee entity
> You can use an effective date (or revision number) to select a summary for a given point in time.

* Route Example: /Employer/ER001/Employee/EE001/Summary
* Data Type: EmployeeSummary
* Property Name: NiLetter

**Example**  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "ER001"
        },
        {
          "@Name": "[EmployeeKey]",
          "@Value": "EE001"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "Employee",
          "@ItemName": "Summary",
          "@Selector": "/Employer/[EmployerKey]/Employee/[EmployeeKey]/Summary",
          "Output": {
            "@xsi:type": "RenderProperty",
            "@Name": "NiLetter",
            "@Property": "NiLetter"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Tax Code and Basis

The tax code and tax basis values are also available on the employee summary data type.

* Route Example: /Employer/ER001/Employee/EE001/Summary
* Data Type: EmployeeSummary
* Property Names: TaxCode, taxBasis

**Example**  

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "ER001"
        },
        {
          "@Name": "[EmployeeKey]",
          "@Value": "EE001"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "Employee",
          "@ItemName": "Summary",
          "@Selector": "/Employer/[EmployerKey]/Employee/[EmployeeKey]/Summary",
          "Output": [
            {
              "@xsi:type": "RenderProperty",
              "@Name": "TaxCode",
              "@Property": "TaxCode"
            },
            {
              "@xsi:type": "RenderProperty",
              "@Name": "TaxBasis",
              "@Property": "TaxBasis"
            }
          ]
        }
      ]
    }
  }
}
```
--/CodeTabs--

### Payments and Deduction Elements.

The contributing factors to an employees remuneration are stored in many different descendants of the **PayLines** entity type.  
The below detailed sections describe a few of the commonly required aspects. 

#### Tax

The employee's PAYE tax liability amount is stored on the **PayLineTax** entity under the **Value** property. 

* Route Example: /Employer/ER001/Employee/EE001/PayLines
* Common Filters: OfType = 'PayLineTax' PaymentDate = '[PayDay]'
* Data Type: PayLineTax
* Property Name: Value

**Example**

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "ER001"
        },
        {
          "@Name": "[EmployeeKey]",
          "@Value": "EE001"
        },
        {
          "@Name": "[PaymentDate]",
          "@Value": "2025-05-31"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "SumOfTax",
          "@Selector": "/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines",
          "Filter": [
            {
              "@xsi:type": "OfType",
              "@Value": "PayLineTax"
            },
            {
              "@xsi:type": "EqualTo",
              "@Property": "PaymentDate",
              "@Value": "[PaymentDate]"
            }
          ],
          "Output": {
            "@xsi:type": "Sum",
            "@Name": "Tax",
            "@Property": "Value",
            "@Negate": "true"
          }
        }
      ]
    }
  }
}
```
--/CodeTabs--

> Note: deduction values are stored as a negative number. This makes calculation of the Net Pay a simple sum of all pay line values. 

#### Employees and Employers National Insurance Contributions.

Both the employees and employers NI contribution values are found on the **PayLineNi** entity type. 

* Route Example: /Employer/ER001/Employee/EE001/PayLines
* Common Filters: OfType = 'PayLineNi' PaymentDate = '[PayDay]'
* Data Type: PayLineNi
* Property Name: 
  * Employee Ni = Value
  * Employer Ni = EmployerNi

**Example**

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "ER001"
        },
        {
          "@Name": "[EmployeeKey]",
          "@Value": "EE001"
        },
        {
          "@Name": "[PaymentDate]",
          "@Value": "2025-05-31"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "Employees",
          "@ItemName": "Employee",
          "@Selector": "/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines",
          "Filter": [
            {
              "@xsi:type": "OfType",
              "@Value": "PayLineNi"
            },
            {
              "@xsi:type": "EqualTo",
              "@Property": "PaymentDate",
              "@Value": "[PaymentDate]"
            }
          ],
          "Output": [
            {
              "@xsi:type": "Sum",
              "@Name": "EENIC",
              "@Property": "Value",
              "@Negate": "true"
            },
            {
              "@xsi:type": "Sum",
              "@Name": "ERNIC",
              "@Property": "EmployerNI",
              "@Negate": "true"
            }
          ]
        }
      ]
    }
  }
}
```
--/CodeTabs--

#### Employees and Employers Pension Contributions.

Both the employees and employers pension contribution values are found on the **PayLinePension** entity type. 

* Route Example: /Employer/ER001/Employee/EE001/PayLines
* Common Filters: OfType = 'PayLinePension' PaymentDate = '[PayDay]'
* Data Type: PayLinePension
* Property Name: 
  * Employee Contribution = Value
  * Employer Contribution = EmployerContribution

**Example**

--CodeTabs--

```json
{
  "Query": {
    "RootNodeName": "ResultSet",
    "Variables": {
      "Variable": [
        {
          "@Name": "[EmployerKey]",
          "@Value": "ER001"
        },
        {
          "@Name": "[EmployeeKey]",
          "@Value": "EE001"
        },
        {
          "@Name": "[PaymentDate]",
          "@Value": "2025-05-31"
        }
      ]
    },
    "Groups": {
      "Group": [
        {
          "@GroupName": "SumOfPensionContribution",
          "@Selector": "/Employer/[EmployerKey]/Employee/[EmployeeKey]/PayLines",
          "Filter": [
            {
              "@xsi:type": "OfType",
              "@Value": "PayLinePension"
            },
            {
              "@xsi:type": "EqualTo",
              "@Property": "PaymentDate",
              "@Value": "[PaymentDate]"
            }
          ],
          "Output": [
            {
              "@xsi:type": "Sum",
              "@Name": "EECont",
              "@Property": "Value",
              "@Negate": "true"
            },
            {
              "@xsi:type": "Sum",
              "@Name": "ERCont",
              "@Property": "EmployerContribution",
              "@Negate": "true"
            }
          ]
        }
      ]
    }
  }
}```
--/CodeTabs--

