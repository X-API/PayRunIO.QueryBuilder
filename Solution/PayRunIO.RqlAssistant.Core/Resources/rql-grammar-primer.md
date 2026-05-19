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

1. **Identify the entity.** Call `list_schemas` (filter by keyword) and `get_schema` to
   confirm property names and types before referencing them.
2. **Identify the route.** Call `list_routes` (filter by URL fragment, verb or tag) and
   `get_route` to copy the exact route URL into the `Selector` attribute.
3. **Look up grammar.** Call `get_rql_syntax(topic)` for any RQL construct you need —
   filter operators, conditions, render types, etc. Use `list_rql_topics` to see the
   index.
4. **Validate.** Call `validate_query(xml)` before finalising. Re-call until
   `IsValid: true`. Diagnostics name the line and column of any issue.

## Rules of thumb

- All references to entity properties use `<Subject ref="PropertyName" />` shape;
  literal values go in `<Object>value</Object>`.
- Filter operators are XML elements (e.g. `<Equal>`, `<GreaterThan>`, `<Contain>`),
  *not* attributes.
- Group selectors always start with `/`.
- XML must be ASCII; no XML comments inside `<Query>`.

If a syntax detail is not in this primer, **do not invent it** — fetch it.
