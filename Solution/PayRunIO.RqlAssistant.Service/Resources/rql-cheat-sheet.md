# RQL Overview Guide

**Report Query Language (RQL)** is a powerful, structured query format used within the PayRun.io API to define ad-hoc and reusable reports. It enables the extraction and transformation of payroll-related data through a declarative, hierarchical structure.

## 🎯 Purpose and Intent

RQL allows users to construct highly customized reports by describing **what data** to retrieve and **how to structure the response**. It supports both flat and nested queries over entities like Employers, Employees, Pay Instructions, and more.

## 🧱 Core Concepts

### Query Structure
An RQL query is composed of the following elements:
- **Root Node** – Defines the name of the top-level result.
- **Variables** – Parameterize and control query behavior.
- **Groups** – Specify entity selection logic and shape the result hierarchy.
- **Outputs** – Define the data to be included in the output for each group.
- **Conditions & Filters** – Apply logic to determine group visibility and post-fetch filtering.

### Groups
Groups are the foundational units of data selection. They:
- Match one or more entities using `Selector` paths.
- Can be nested to represent parent-child relationships.
- Include `Output`, `Condition`, `Filter`, and `Ordering` elements.

### Selectors
Selectors define the resource path (similar to an API URI) for the data to be queried. They can be:
- **Static paths** (e.g. `/Employers`)
- **Dynamic paths** using variable substitution (e.g. `/Employer/[EmployerKey]/Employees`)

### Variables
Variables are used:
- As dynamic placeholders in selectors or outputs.
- To store intermediate values like totals or identifiers.
- With optional formatting and runtime reassignment via outputs.

### Outputs
Outputs specify what information to return:
- Single-value outputs (like entity fields or constants).
- Aggregate outputs (such as `Sum`, `Count`, `Avg`).
- Render control (e.g. to elements, attributes, or variables).

### Conditions and Filters
- **Conditions** determine whether a group executes.
- **Filters** constrain entity sets after selection.
- **Predicates** can be used for pre-fetch constraints within a `Selector`.

## 💡 Advanced Capabilities
- **Nested Groups** for hierarchical traversal and output.
- **Render Options** to customize output structure and behavior.
- **Expression Calculators** for in-query arithmetic.
- **Support for Date/Number formatting, looping, tax period calculations, and regex substitution.**

## 🔄 Execution Model
- RQL processes from top-level groups downward.
- Each group loops through matched entities.
- Variables and outputs are evaluated per match.
- Nested groups reflect the parent-child traversal of entity hierarchies.

## ✅ Typical Use Cases
- Payroll summaries and breakdowns.
- Dynamic report generation without code changes.
- Bulk querying of nested employee/pay instruction data.
