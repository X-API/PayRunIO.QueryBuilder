# Report Query Language (RQL) – Condensed Spec (v2025)

**Use** this cheat‑sheet when generating RQL inside an LLM prompt. It contains every rule once; deeper payroll‑domain look‑ups live in the retrieval appendix.

---

## 1. Mini‑grammar

```xml
<Query RootNodeName="…">
  <Variables>* <Variable Name="…" Value="…"/> </Variables>
  <Groups>+
    <Group GroupName="…" Selector="…" [ItemName] [UniqueKeyVariable]
           [Predicate] [LoopExpression]>
      <Conditions>*  <Condition Type="…" …/> </Conditions>
      <Filters>*     <Filter Type="…" …/>    </Filters>
      <Ordering>?    <OrderBy Property="…" Direction="Ascending|Descending"/> </Ordering>
      <Outputs>*     <!-- RenderEntity | Sum | … --> </Outputs>
      <Groups>*      <!-- recursion -->
    </Group>
  </Groups>
</Query>
```

Legend: `*` = 0‑n, `+` = 1‑n, `?` = 0‑1, `[]` = optional attribute.

---

## 2. Core attributes (groups & query)

| Attr | Default | Purpose | Snippet |
|------|---------|---------|---------|
| `GroupName` | – | Node name in output | `GroupName="Employer"` |
| `ItemName`  | plural of GroupName | Rename single items | `ItemName="Employee"` |
| `Selector`  | – | Path to data | `/Employer/[EmployerKey]/Employees` |
| `UniqueKeyVariable` | – | Stores ID of each match | `UniqueKeyVariable="[EmpID]"` |
| `Predicate` | – | Server‑side SQL filter | `Predicate="EmployeeID > 1000"` |
| `LoopExpression` | – | For each CSV/array item | `LoopExpression="[YearList]"` |
| `RootNodeName` (Query) | `QueryResult` | Top element name | `RootNodeName="PayrollRpt"` |

---

## 3. Outputs & filters

| Type | Key params | Returns / Action |
|------|------------|------------------|
| **RenderEntity** | `Output` target | Whole XML of entity |
| **RenderProperty** | `Property` | Value of field |
| **RenderValue** | `Value`, `Regex` | Constant or extracted text |
| **Sum / Count / Avg / Min / Max** | `Property` | Aggregate number |
| **Distinct** | `Property` | CSV of unique values |
| **ExpressionCalculator** | `Expression` | Calculated scalar |
| **Filter.Equals / NotEquals** | `Property`, `Value` | Match (= / ≠) |
| **Filter.GreaterThan / LessThan / Between** | `Property`, `Value(s)` | Range |
| **Filter.Contains / StartsWith / Regex** | `Property`, `Value` | Text test |
| **Filter.OfType / NotOfType** | `Property`, `Type` | Polymorphic entity |
| **Filter.FlagSet / FlagNotSet** | `Property`, `Flag` | Bitwise flag |
| **Filter.IsNull / NotNull** | `Property` | Null check |
| **Filter.TakeFirst** | `Count` | Cardinality limit |

All filters AND together unless `<Filters IsOr="true">`.

`Output=` can be `Element` (default), `Attribute`, `Variable`, `VariableSum/Append/Prepend`, or `InnerText`.

---

## 4. Evaluation & paging rules

* **Order**: Conditions → Selector → Predicate → Filters → Ordering → *aggregates* → scalars.  
* Variables are **global for the whole query**; re‑initialise them in each loop to avoid leakage.  
* `[StartIndex]` & `[MaxIndex]` variables plus `RenderIndex` enable paging.  
* Wildcard `*` steps and `//` segments flatten selectors; use sparingly for performance.

---

## 5. Golden‑path examples

### 5.1 Flat list
```xml
<Query RootNodeName="Employers">
  <Groups>
    <Group GroupName="Employer" Selector="/Employer">
      <Outputs>
        <RenderProperty Property="EmployerName"/>
      </Outputs>
    </Group>
  </Groups>
</Query>
```

### 5.2 Nested hierarchy with variable chaining
```xml
<Query RootNodeName="PayDetails">
  <Groups>
    <Group GroupName="Employer" Selector="/Employer" UniqueKeyVariable="[EmpKey]">
      <Groups>
        <Group GroupName="Employee" Selector="/Employer/[EmpKey]/Employees" UniqueKeyVariable="[EEKey]">
          <Groups>
            <Group GroupName="PayLine" Selector="/Employer/[EmpKey]/Employees/[EEKey]/PayLines">
              <Outputs>
                <RenderProperty Property="Description"/>
                <RenderProperty Property="Amount"/>
              </Outputs>
            </Group>
          </Groups>
        </Group>
      </Groups>
    </Group>
  </Groups>
</Query>
```

### 5.3 Aggregation
```xml
<Query RootNodeName="Totals">
  <Groups>
    <Group GroupName="Employer" Selector="/Employer">
      <Outputs>
        <Sum Property="GrossPay" Output="Element"/>
        <Count Property="Employees" Output="Attribute"/>
      </Outputs>
    </Group>
  </Groups>
</Query>
```

---

**Appendix** – Full filter catalogue, payroll field map, and optimisation flags are stored separately in the vector index. Fetch them only when the user question references a specific item not covered above.

---

*End of condensed specification (≈ 600 tokens).*