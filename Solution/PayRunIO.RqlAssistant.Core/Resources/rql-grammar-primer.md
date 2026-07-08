# RQL Primer

RQL (Report Query Language) is a PayRunIO-specific **XML** DSL for retrieving structured
data from the PayRunIO UK payroll API. The root element is always `<Query>`. The model
**is not expected to know RQL from training data** — call the tools provided.

## Minimal shape

```xml
<Query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <RootNodeName>MyReport</RootNodeName>
  <Groups>
    <Group GroupName="Employees" ItemName="Employee" Selector="/Employer/{employerId}/Employees">
      <Output xsi:type="RenderEntity" />
    </Group>
  </Groups>
</Query>
```

- **`<RootNodeName>`** — alphanumeric, no spaces. Names the wrapping element in the result.
- **`<Groups>`** holds one or more `<Group>` elements. Groups can nest.
- **`<Group>`** has three required attributes: `GroupName` (the result element name),
  `ItemName` (singular form used inside the group), and `Selector` (the resource path,
  taken from a PayRunIO API route — use `list_routes` / `get_route` to find it).
- **`<Output>`** has `xsi:type` set to one of the supported render types (RenderEntity,
  RenderProperty, RenderValue, Count, Sum, Avg, Max, Min, Distinct, etc.) — see the
  `outputs` topic.

## Common building blocks (call `get_rql_syntax` for details)

| What you want                     | Topic                              |
| --------------------------------- | ---------------------------------- |
| Filter rows                       | `filters`                          |
| Order results                     | `ordering`                         |
| Branch on a value                 | `conditions-and-conditional-group-logic` |
| Aggregate (Sum/Avg/Count/Distinct)| `outputs`                          |
| Variables and substitution        | `variables`                        |
| Loop over a date range            | `loop-expressions`                 |
| Tabular output                    | `advanced-techniques`              |
| Direct DB-level predicates        | `advanced-features-pt1`            |

## How to work

1. **Check the example bank.** Call `list_examples` (filter by keyword) to find a
   validated example close to the request, then `get_example(slug)` and adapt it.
   Adapting a known-good example beats free composition.
2. **Identify the entity.** Call `list_schemas` (filter by keyword) and `get_schema` to
   confirm property names and types before referencing them.
3. **Identify the route.** Call `list_routes` (filter by URL fragment, verb or tag) and
   `get_route` to copy the exact route URL into the `Selector` attribute.
4. **Look up grammar.** Call `get_rql_syntax(topic)` for any RQL construct you need —
   filter operators, conditions, render types, etc. Use `list_rql_topics` to see the
   index.
5. **Validate.** Call `validate_query(xml)` before finalising. Re-call until
   `IsValid: true`. Diagnostics name the line and column of any issue.

## Rules of thumb

- Filters, conditions, outputs and orders are all `xsi:type`-discriminated elements with
  attribute arguments, e.g. `<Filter xsi:type="EqualTo" Property="LastName" Value="Smith" />`,
  `<Output xsi:type="Sum" Name="Total" Property="Value" />`.
- Group children must appear in XSD sequence order: `<Condition>`, `<Filter>`, `<Output>`,
  `<Order>`, then nested `<Group>` elements. All outputs come **before** sub-groups; to
  render values *after* nested aggregation, put the render outputs in a trailing sub-group.
- Group selectors always start with `/`. Variables in square brackets (e.g. `[EmployerKey]`)
  are substituted into selectors, predicates, filter values and output values.
- Initialise any variable written by `Sum`/`VariableSum` at the start of each iteration,
  or the previous iteration's value leaks into rows with no matches.
- XML must be ASCII; no XML comments inside `<Query>`.

If a syntax detail is not in this primer, **do not invent it** — fetch it.
