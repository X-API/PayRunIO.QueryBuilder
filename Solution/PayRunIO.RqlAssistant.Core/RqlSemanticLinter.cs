namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;

    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Layer-2 semantic checks over a schema-valid RQL query, using the same route and entity
    /// data the lookup tools expose. Catches the mistakes XSD validation cannot: selectors that
    /// match no API route, property references that exist on no entity, and variables that are
    /// used but never assigned. All diagnostics are warnings — these checks are heuristic and
    /// must never block a query the engine would accept.
    /// </summary>
    public interface IRqlSemanticLinter
    {
        IReadOnlyList<ValidationDiagnostic> Lint(string xml);
    }

    public sealed class RqlSemanticLinter : IRqlSemanticLinter
    {
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        private static readonly Regex VariableToken = new Regex(@"\[[A-Za-z0-9_]+\]", RegexOptions.Compiled);

        /// <summary>
        /// Matches OFTYPE equality comparisons in a group's Predicate attribute, e.g.
        /// <c>OFTYPE = 'PayLineHoliday'</c>. Like an OfType filter, these pin the entity type(s)
        /// the group's property references should be checked against.
        /// </summary>
        private static readonly Regex PredicateOfType = new Regex(@"OFTYPE\s*=\s*'(?<type>[^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Filter types that pin the entity type(s) a group's property references resolve against.
        /// OfDerivedType matches the named type and its subtypes; for property checking both are
        /// treated the same, since the named type's own properties are the ones guaranteed present.
        /// </summary>
        private static readonly string[] TypePinningFilters = { "OfType", "OfDerivedType" };

        /// <summary>
        /// Determines whether a Filter element pins the entity type in scope.
        /// </summary>
        private static bool IsTypePinningFilter(XElement filter) =>
            TypePinningFilters.Contains(TypeOf(filter), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Splits a type filter's Value into individual type names. The engine treats the value as a
        /// comma separated list and matches an entity whose type appears anywhere in it, so
        /// <c>Value="PayLineSmp,PayLineSsp"</c> pins two types rather than naming one.
        /// </summary>
        private static IEnumerable<string> SplitTypeNames(string? value) =>
            (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0);

        /// <summary>
        /// Attributes whose values undergo variable substitution and should be scanned for uses.
        /// Name-like attributes (Name, DisplayName, GroupName, UniqueKeyVariable) are targets, not uses.
        /// </summary>
        private static readonly string[] SubstitutableAttributes =
            {
                "Selector", "Predicate", "Value", "Value2", "Expression",
                "ValueA", "ValueB", "Date", "TaxYear", "TaxPeriod", "PayFrequency", "Href"
            };

        private static readonly string[] VariableOutputRenderTypes =
            {
                "Variable", "VariableSum", "VariableAppend", "VariablePrepend"
            };

        /// <summary>
        /// The variable every loop expression sets to the current iteration value.
        /// </summary>
        private const string LoopVariable = "[LoopVariable]";

        /// <summary>
        /// Loop expressions that set their own named variables per iteration instead of
        /// <see cref="LoopVariable"/>. Keyed by expression name, matched case insensitively.
        /// <c>AllTaxMonths</c> exposes the tax period and its date bounds; the value driven forms
        /// (<c>CSV:</c>, <c>Range:</c>) and <c>AllPaySchedulePeriods</c> have no such variables and
        /// fall back to <see cref="LoopVariable"/>.
        /// </summary>
        private static readonly Dictionary<string, string[]> LoopExpressionVariables =
            new(StringComparer.OrdinalIgnoreCase)
                {
                    ["AllTaxMonths"] = new[] { "[TaxPeriod]", "[TaxPeriodStart]", "[TaxPeriodEnd]" },
                };

        /// <summary>
        /// Output types that render once per matched entity (as opposed to aggregates such as Sum,
        /// which collapse the group to a single value). Rendering these directly from a collection
        /// selector inside a table row produces a variable number of columns.
        /// </summary>
        private static readonly string[] PerEntityRenderTypes =
            {
                "RenderProperty", "RenderEntity", "RenderLink", "RenderTypeName",
                "RenderUniqueKeyFromLink", "RenderIndex", "RenderTagValue"
            };

        private readonly IDocumentRepository repository;

        private readonly object syncLock = new object();

        private IReadOnlyList<(RouteDefinition Route, string[] Segments)>? getRoutes;

        private HashSet<string>? allPropertyNames;

        public RqlSemanticLinter(IDocumentRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IReadOnlyList<ValidationDiagnostic> Lint(string xml)
        {
            var diagnostics = new List<ValidationDiagnostic>();

            if (string.IsNullOrWhiteSpace(xml))
            {
                return diagnostics;
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(xml, LoadOptions.SetLineInfo);
            }
            catch (XmlException)
            {
                // Malformed XML is the XSD validator's diagnostic to report, not ours.
                return diagnostics;
            }

            var root = document.Root;
            if (root == null || root.Name.LocalName != "Query")
            {
                return diagnostics;
            }

            this.CheckSelectors(root, diagnostics);
            this.CheckPropertyReferences(root, diagnostics);
            CheckMetaDataReferences(root, diagnostics);
            CheckVariableAssignments(root, diagnostics);
            CheckLoopVariableUsage(root, diagnostics);
            CheckEntityLessGroupOperations(root, diagnostics);
            this.CheckTabularCollectionRenders(root, diagnostics);
            CheckTabularRowsPlacement(root, diagnostics);

            return diagnostics;
        }

        private void CheckSelectors(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            foreach (var group in root.Descendants("Group"))
            {
                var segments = SelectorSegments(group);

                if (segments == null)
                {
                    continue;
                }

                if (this.MatchRoutes(segments).Count > 0)
                {
                    continue;
                }

                diagnostics.Add(Warn(
                    group,
                    "UnknownRoute",
                    $"Selector '{group.Attribute("Selector")?.Value}' does not match any known GET API route. "
                    + "Variables like [Key] only substitute into route parameter slots such as {employerId}. "
                    + "Use list_routes to find the correct URL template."));
            }
        }

        /// <summary>
        /// Returns the group's selector split into segments, or null when the selector is absent or
        /// starts with a variable — a selector that is (or starts with) a variable can resolve to
        /// anything and is unknowable statically.
        /// </summary>
        private static string[]? SelectorSegments(XElement group)
        {
            var selector = group.Attribute("Selector")?.Value;

            if (string.IsNullOrWhiteSpace(selector) || !selector.StartsWith('/'))
            {
                return null;
            }

            return selector.Trim('/').Split('/');
        }

        private void CheckPropertyReferences(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            foreach (var group in root.Descendants("Group"))
            {
                // OfType/OfDerivedType filters and OFTYPE predicate comparisons pin the entity
                // type(s) in scope; property checks become exact.
                var ofTypeSchemas = group.Elements("Filter")
                    .Where(IsTypePinningFilter)
                    .SelectMany(f => SplitTypeNames(f.Attribute("Value")?.Value))
                    .Concat(PredicateOfType
                        .Matches(group.Attribute("Predicate")?.Value ?? string.Empty)
                        .SelectMany(m => SplitTypeNames(m.Groups["type"].Value)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var scopedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var scopeKnown = ofTypeSchemas.Length > 0;
                var scopeNames = ofTypeSchemas;

                foreach (var typeName in ofTypeSchemas)
                {
                    var schema = this.repository.GetSchema(typeName);

                    if (schema == null)
                    {
                        diagnostics.Add(Warn(
                            group,
                            "UnknownEntityType",
                            $"The OfType filter or OFTYPE predicate names '{typeName}' which matches no known entity schema. Use list_schemas to find the correct type name."));
                        scopeKnown = false;
                        continue;
                    }

                    foreach (var property in schema.Properties ?? Enumerable.Empty<Models.PropertyDefinition>())
                    {
                        if (property.Name != null)
                        {
                            scopedProperties.Add(property.Name);
                        }
                    }
                }

                // No OfType: try to pin the scope from the entity type the selector's route returns.
                if (ofTypeSchemas.Length == 0)
                {
                    var routeEntity = this.ResolveSelectorEntity(group);

                    if (routeEntity != null)
                    {
                        scopeKnown = true;
                        scopeNames = new[] { routeEntity.ClassName ?? string.Empty };

                        foreach (var property in routeEntity.Properties ?? Enumerable.Empty<Models.PropertyDefinition>())
                        {
                            if (property.Name != null)
                            {
                                scopedProperties.Add(property.Name);
                            }
                        }
                    }
                }

                foreach (var element in group.Elements().Where(e => e.Name.LocalName is "Output" or "Filter" or "Order"))
                {
                    var property = element.Attribute("Property")?.Value;

                    // Skip absent, variable-substituted and dotted-path references.
                    if (string.IsNullOrWhiteSpace(property) || property.Contains('[') || property.Contains('.'))
                    {
                        continue;
                    }

                    // An output may carry its own OfType filter, narrowing the matched set for that
                    // aggregate alone. When present it overrides the group scope, so properties of
                    // the narrowed subtype resolve rather than falsely reporting as unknown.
                    var elementScope = this.ResolveOutputOfTypeScope(element, diagnostics);
                    var effectiveKnown = elementScope?.Known ?? scopeKnown;
                    var effectiveProperties = elementScope?.Properties ?? scopedProperties;
                    var effectiveNames = elementScope?.Names ?? scopeNames;

                    if (effectiveKnown)
                    {
                        if (!effectiveProperties.Contains(property))
                        {
                            diagnostics.Add(Warn(
                                element,
                                "UnknownProperty",
                                $"Property '{property}' does not exist on {string.Join("/", effectiveNames)} (the entity type selected by this group). Use get_schema to confirm property names."));
                        }
                    }
                    else if (!this.GetAllPropertyNames().Contains(property))
                    {
                        diagnostics.Add(Warn(
                            element,
                            "UnknownProperty",
                            $"Property '{property}' does not exist on any known entity schema. Use get_schema to confirm property names."));
                    }
                }
            }
        }

        /// <summary>
        /// Resolves the property scope contributed by an output's own child OfType filters, or null
        /// when the output carries none and the enclosing group scope should be used instead.
        /// </summary>
        /// <remarks>
        /// An Output may nest its own Filter elements to narrow the matched set for that aggregate
        /// alone, letting one selector feed several differently-filtered aggregates in a single
        /// traversal. A nested OfType therefore pins a subtype whose properties need not exist on the
        /// type the group selector yields, e.g. summing PayLineTax.TaxablePay over a PayLines group.
        /// </remarks>
        private PropertyScope? ResolveOutputOfTypeScope(XElement element, List<ValidationDiagnostic> diagnostics)
        {
            var typeNames = element.Elements("Filter")
                .Where(IsTypePinningFilter)
                .SelectMany(f => SplitTypeNames(f.Attribute("Value")?.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (typeNames.Length == 0)
            {
                return null;
            }

            var properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var known = true;

            foreach (var typeName in typeNames)
            {
                var schema = this.repository.GetSchema(typeName);

                if (schema == null)
                {
                    diagnostics.Add(Warn(
                        element,
                        "UnknownEntityType",
                        $"The OfType filter or OFTYPE predicate names '{typeName}' which matches no known entity schema. Use list_schemas to find the correct type name."));
                    known = false;
                    continue;
                }

                foreach (var property in schema.Properties ?? Enumerable.Empty<Models.PropertyDefinition>())
                {
                    if (property.Name != null)
                    {
                        properties.Add(property.Name);
                    }
                }
            }

            return new PropertyScope(known, properties, typeNames);
        }

        /// <summary>
        /// A resolved set of property names valid for a scope, along with the entity type names that
        /// contributed them and whether every one of those types resolved to a known schema.
        /// </summary>
        private sealed record PropertyScope(bool Known, HashSet<string> Properties, string[] Names);

        /// <summary>
        /// Resolves the entity schema a group's selector yields, or null when it cannot be pinned to
        /// exactly one type. Single-entity routes name their type in ResponseType; collection routes
        /// report 'LinkCollection', so the type is inferred by singularising the last literal route
        /// segment (Employees → Employee). Ambiguity across candidate routes falls back to null.
        /// </summary>
        private ClassDefinition? ResolveSelectorEntity(XElement group)
        {
            var segments = SelectorSegments(group);

            if (segments == null)
            {
                return null;
            }

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var route in this.MatchRoutes(segments))
            {
                var schema = this.ResolveRouteEntity(route);

                if (schema?.ClassName == null)
                {
                    // A matched route with no resolvable entity keeps the scope unknowable.
                    return null;
                }

                candidates.Add(schema.ClassName);
            }

            return candidates.Count == 1 ? this.repository.GetSchema(candidates.Single()) : null;
        }

        private ClassDefinition? ResolveRouteEntity(RouteDefinition route)
        {
            var responseType = route.ResponseType;

            if (!string.IsNullOrWhiteSpace(responseType)
                && !string.Equals(responseType, "LinkCollection", StringComparison.OrdinalIgnoreCase))
            {
                // Strip namespace qualifiers such as 'Models.Tag'.
                var typeName = responseType.Contains('.') ? responseType[(responseType.LastIndexOf('.') + 1)..] : responseType;
                var schema = this.repository.GetSchema(typeName);

                if (schema != null)
                {
                    return schema;
                }
            }

            var lastLiteral = route.RouteSignature?
                .Trim('/')
                .Split('/')
                .LastOrDefault(s => !s.StartsWith('{') && !s.Contains('('));

            if (string.IsNullOrWhiteSpace(lastLiteral))
            {
                return null;
            }

            foreach (var candidate in SingularCandidates(lastLiteral))
            {
                var schema = this.repository.GetSchema(candidate);

                if (schema != null)
                {
                    return schema;
                }
            }

            return null;
        }

        private static IEnumerable<string> SingularCandidates(string plural)
        {
            yield return plural;

            if (plural.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            {
                yield return plural[..^3] + "y";
            }

            if (plural.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                yield return plural[..^1];
            }
        }

        /// <summary>
        /// Matches an attempt to navigate the meta data item collection, e.g. <c>MetaData.Items</c>
        /// or <c>MetaData.Item[0].Value</c>. RQL exposes items as pseudo properties
        /// (<c>MetaData.CostCentre</c>) and cannot traverse the underlying collection, but the
        /// generated schema advertises an <c>Items</c> member of type
        /// <c>Collection&lt;MetaDataItem&gt;</c>, which makes this a natural mistake to write.
        /// </summary>
        private static readonly Regex MetaDataItemsNavigation =
            new Regex(@"\bMetaData\s*\.\s*Items?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Matches a reference to the <c>AllItemNames</c> meta data extension.
        /// </summary>
        private static readonly Regex MetaDataAllItemNames =
            new Regex(@"\bMetaData\s*\.\s*AllItemNames\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Attributes that carry a property path or expression in which a meta data reference can appear.
        /// </summary>
        private static readonly string[] MetaDataBearingAttributes =
            { "Property", "Predicate", "Expression", "Value", "Value2", "ValueA", "ValueB" };

        /// <summary>
        /// Flags the two meta data mistakes the property checker cannot see, because it skips every
        /// dotted path: navigating the item collection (never valid), and using
        /// <c>AllItemNames</c> in a group Predicate (not translatable to SQL, so the engine cannot
        /// evaluate it at the database).
        /// </summary>
        private static void CheckMetaDataReferences(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            foreach (var element in root.DescendantsAndSelf())
            {
                foreach (var attribute in element.Attributes())
                {
                    if (!MetaDataBearingAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    if (MetaDataItemsNavigation.IsMatch(attribute.Value))
                    {
                        diagnostics.Add(Warn(
                            element,
                            "MetaDataItemsNavigation",
                            $"'{attribute.Name.LocalName}' navigates the meta data item collection, which RQL cannot do. "
                            + "Meta data values are read with the item name as a pseudo property of the MetaData sub "
                            + "element, e.g. Property=\"MetaData.CostCentre\". To work with item names dynamically use "
                            + "\"MetaData.AllItemNames\" (a comma separated list of every item name). "
                            + "See get_rql_syntax('meta-data')."));
                    }

                    // AllItemNames has no SQL equivalent, so it cannot be evaluated in a predicate.
                    if (attribute.Name.LocalName == "Predicate" && MetaDataAllItemNames.IsMatch(attribute.Value))
                    {
                        diagnostics.Add(Warn(
                            element,
                            "MetaDataAllItemNamesInPredicate",
                            "'MetaData.AllItemNames' cannot be used in a group Predicate: predicates are translated "
                            + "into direct SQL clauses and this RQL extension has no SQL equivalent. Move the test to a "
                            + "<Filter xsi:type=\"Contain\" Property=\"MetaData.AllItemNames\" Value=\"ItemName\" />, "
                            + "which is evaluated in memory, or test a concrete item instead "
                            + "(Predicate=\"MetaData.ItemName != null\")."));
                    }
                }
            }
        }

        /// <summary>
        /// A group with no Selector and no LoopExpression matches no entities, so Order and Filter
        /// elements inside it silently do nothing — a frequent generation mistake (e.g. attempting
        /// to sort the report by adding an Order to a trailing empty group).
        /// </summary>
        private static void CheckEntityLessGroupOperations(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            foreach (var group in root.Descendants("Group"))
            {
                var hasEntityScope = !string.IsNullOrWhiteSpace(group.Attribute("Selector")?.Value)
                                     || !string.IsNullOrWhiteSpace(group.Attribute("LoopExpression")?.Value);

                if (hasEntityScope)
                {
                    continue;
                }

                foreach (var element in group.Elements().Where(e => e.Name.LocalName is "Order" or "Filter"))
                {
                    var kind = element.Name.LocalName;

                    diagnostics.Add(Warn(
                        element,
                        kind == "Order" ? "OrderInEntityLessGroup" : "FilterInEntityLessGroup",
                        $"<{kind}> appears in a group with no Selector or LoopExpression, so it has no entities to "
                        + $"{(kind == "Order" ? "order" : "filter")} and is silently ignored. Move it into the group "
                        + "whose Selector loads the entities it should apply to."));
                }
            }
        }

        /// <summary>
        /// In a Table query every row must render the same number of 'col' values. A nested group
        /// whose selector returns a collection and which renders per-entity outputs directly emits
        /// one value per matched entity, so column counts drift with the data. The value should be
        /// captured into a variable (or the group narrowed with Order + TakeFirst) instead.
        /// </summary>
        private void CheckTabularCollectionRenders(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            if (!string.Equals(root.Element("RootNodeName")?.Value, "Table", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var group in root.Descendants("Group").Where(g => g.Parent?.Name.LocalName == "Group"))
            {
                var segments = SelectorSegments(group);

                if (segments == null)
                {
                    continue;
                }

                var matchedRoutes = this.MatchRoutes(segments);

                // Unknown routes are already reported; only warn when every possible match is a collection.
                if (matchedRoutes.Count == 0
                    || !matchedRoutes.All(r => string.Equals(r.ResponseType, "LinkCollection", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (group.Elements("Filter").Any(f => string.Equals(TypeOf(f), "TakeFirst", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                foreach (var output in group.Elements("Output"))
                {
                    var renderType = TypeOf(output);
                    var target = output.Attribute("Output")?.Value;

                    if (renderType == null
                        || !PerEntityRenderTypes.Contains(renderType, StringComparer.OrdinalIgnoreCase)
                        || (target != null && VariableOutputRenderTypes.Contains(target, StringComparer.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    diagnostics.Add(Warn(
                        output,
                        "CollectionRenderInTableRow",
                        $"This {renderType} output renders once per entity matched by the collection selector "
                        + $"'{group.Attribute("Selector")?.Value}', so the number of rendered values varies with the data and "
                        + "table columns will not line up. Capture the value into a variable (Output=\"Variable\") and render "
                        + "it from a final entity-less group, or narrow this group to one entity with an <Order> plus "
                        + "<Filter xsi:type=\"TakeFirst\" Value=\"1\" />."));
                }
            }
        }

        /// <summary>
        /// A Table query's downstream consumers (CSV export, the report table view) read the row data
        /// from a single "Rows" group of "Row" items directly under the root. Wrapping that group inside
        /// an outer named/item-named group — a common way to express "one section per schedule/employer" —
        /// still validates and lints clean, but nests the rows under wrapper elements
        /// (Table > Schedules > Schedule > Rows > Row) so the flat tabular reader finds no rows and the
        /// export comes out empty. The correct shape iterates the outer entity with an un-named group that
        /// captures keys into variables, or folds that iteration into the Rows selector itself.
        /// </summary>
        private static void CheckTabularRowsPlacement(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            if (!string.Equals(root.Element("RootNodeName")?.Value, "Table", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var groups = root.Element("Groups");

            if (groups == null)
            {
                return;
            }

            var rowsGroups = root
                .Descendants("Group")
                .Where(g => string.Equals(g.Attribute("GroupName")?.Value, "Rows", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rowsGroups.Count == 0)
            {
                diagnostics.Add(Warn(
                    groups,
                    "TabularMissingRowsGroup",
                    "This Table query has no group named 'Rows'. The tabular report reader expects a single "
                    + "<Group GroupName=\"Rows\" ItemName=\"Row\"> directly under the root <Groups>, following the "
                    + "static Headers group; each data row is a 'Row' item it emits. Without it the exported table "
                    + "has headers but no rows."));
                return;
            }

            foreach (var rows in rowsGroups)
            {
                if (rows.Parent == groups)
                {
                    continue;
                }

                var wrapper = rows.Ancestors("Group").FirstOrDefault();
                var wrapperName = wrapper?.Attribute("GroupName")?.Value
                                  ?? wrapper?.Attribute("ItemName")?.Value
                                  ?? "an outer group";

                diagnostics.Add(Warn(
                    rows,
                    "TabularRowsNested",
                    $"The 'Rows' group is nested inside '{wrapperName}' rather than sitting directly under the root "
                    + "<Groups>. That wrapper emits its own container elements around every row, so the flat tabular "
                    + "reader (CSV export and table view) finds no rows and the report comes out empty. To vary rows by "
                    + "an outer entity, iterate it in the Rows selector itself, or wrap the iteration in an un-named "
                    + "<Group> (no GroupName/ItemName) that captures the outer key into a variable and reference it "
                    + "from the Rows selector — keep exactly one 'Rows'/'Row' group directly under <Groups>."));
            }
        }

        private static void CheckVariableAssignments(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            var assigned = new HashSet<string>(StringComparer.Ordinal);

            foreach (var variable in root.Elements("Variables").Elements("Variable"))
            {
                var name = variable.Attribute("Name")?.Value;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    assigned.Add(name);
                }
            }

            foreach (var variable in root.Elements("Required").Elements("Variable"))
            {
                if (!string.IsNullOrWhiteSpace(variable.Value))
                {
                    assigned.Add(variable.Value.Trim());
                }
            }

            foreach (var group in root.Descendants("Group"))
            {
                var uniqueKey = group.Attribute("UniqueKeyVariable")?.Value;
                if (!string.IsNullOrWhiteSpace(uniqueKey))
                {
                    assigned.Add(uniqueKey);
                }
            }

            foreach (var output in root.Descendants("Output"))
            {
                var renderTarget = output.Attribute("Output")?.Value;

                if (renderTarget != null
                    && VariableOutputRenderTypes.Contains(renderTarget, StringComparer.OrdinalIgnoreCase))
                {
                    // Most render types name their target in 'Name'; date renders such as
                    // RenderTaxPeriodDate use 'DisplayName' instead.
                    foreach (var targetAttribute in new[] { "Name", "DisplayName" })
                    {
                        var name = output.Attribute(targetAttribute)?.Value;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            assigned.Add(name);
                        }
                    }

                    // RenderLink variable output writes to the reserved [Link] variable.
                    if (string.Equals(TypeOf(output), "RenderLink", StringComparison.OrdinalIgnoreCase))
                    {
                        assigned.Add("[Link]");
                    }
                }
            }

            foreach (var group in root.Descendants("Group"))
            {
                var loopExpression = group.Attribute("LoopExpression")?.Value;

                if (loopExpression == null)
                {
                    continue;
                }

                foreach (var name in VariablesSetByLoopExpression(loopExpression))
                {
                    assigned.Add(name);
                }
            }

            var reported = new HashSet<string>(StringComparer.Ordinal);

            foreach (var element in root.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    if (!SubstitutableAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    foreach (Match token in VariableToken.Matches(attribute.Value))
                    {
                        if (assigned.Contains(token.Value) || !reported.Add(token.Value))
                        {
                            continue;
                        }

                        diagnostics.Add(Warn(
                            element,
                            "UnassignedVariable",
                            $"Variable '{token.Value}' is used but never assigned (no <Variables> entry, <Required> declaration, UniqueKeyVariable, or variable output writes it). Substitution will leave the placeholder text as-is."));
                    }
                }
            }
        }

        /// <summary>
        /// Flags <see cref="LoopVariable"/> used inside a group whose loop expression does not set
        /// it. <c>AllTaxMonths</c> is the case that matters: it sets named variables instead, so a
        /// query written against the usual <c>[LoopVariable]</c> convention renders the literal
        /// placeholder text in every row rather than failing outright. The generic unassigned
        /// variable warning names the problem but not the fix, so this names the replacements.
        /// </summary>
        private static void CheckLoopVariableUsage(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            foreach (var group in root.Descendants("Group"))
            {
                var loopExpression = group.Attribute("LoopExpression")?.Value;

                if (loopExpression == null
                    || VariablesSetByLoopExpression(loopExpression).Contains(LoopVariable, StringComparer.Ordinal))
                {
                    continue;
                }

                var name = loopExpression.Split(':')[0].Trim();
                var replacements = string.Join(", ", VariablesSetByLoopExpression(loopExpression));

                // The loop variable is in scope for the whole group, including nested groups — but
                // a nested group with its own loop expression rebinds it, so stop at that boundary.
                foreach (var element in group.DescendantsAndSelf())
                {
                    if (element != group && element.Name.LocalName == "Group"
                                         && element.Attribute("LoopExpression") != null)
                    {
                        continue;
                    }

                    if (element.Ancestors("Group").TakeWhile(a => a != group).Any(a => a.Attribute("LoopExpression") != null))
                    {
                        continue;
                    }

                    foreach (var attribute in element.Attributes())
                    {
                        if (!SubstitutableAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal)
                            || !attribute.Value.Contains(LoopVariable, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        diagnostics.Add(Warn(
                            element,
                            "LoopVariableNotSet",
                            $"'{LoopVariable}' is used inside a '{name}' loop, which does not set it. "
                            + $"{name} sets {replacements} instead — use those, or the placeholder text is "
                            + "rendered literally in every iteration."));
                    }
                }
            }
        }

        /// <summary>
        /// The variables a loop expression assigns on each iteration.
        ///
        /// Most expressions expose the current value as <see cref="LoopVariable"/>. Some instead
        /// set their own named variables — <c>AllTaxMonths</c> sets the tax period and its start and
        /// end dates — and for those <see cref="LoopVariable"/> is not populated, so a query using
        /// it would silently render the literal placeholder text.
        /// </summary>
        /// <param name="loopExpression">The group's LoopExpression attribute value.</param>
        private static IEnumerable<string> VariablesSetByLoopExpression(string loopExpression)
        {
            // Value driven forms carry their data after a colon ("CSV:a,b,c", "Range:1-52"), so the
            // expression name is whatever precedes it.
            var name = loopExpression.Split(':')[0].Trim();

            return LoopExpressionVariables.TryGetValue(name, out var variables)
                       ? variables
                       : new[] { LoopVariable };
        }

        /// <summary>
        /// A selector matches a route when segment counts are equal and each pair matches:
        /// route parameters (<c>{...}</c>) accept variables, wildcards and any literal satisfying
        /// their type constraint (e.g. <c>{effectiveDate:datetime(yyyy-MM-dd)}</c> rejects the
        /// literal 'PayRuns'); selector variables (<c>[Var]</c>) substitute key values so they only
        /// match route parameter slots, never literals; literals must match case-insensitively.
        /// </summary>
        private static bool SegmentsMatch(string[] routeSegments, string[] selectorSegments)
        {
            if (routeSegments.Length != selectorSegments.Length)
            {
                return false;
            }

            for (var i = 0; i < routeSegments.Length; i++)
            {
                var routeSegment = routeSegments[i];
                var selectorSegment = selectorSegments[i];

                if (routeSegment.StartsWith('{'))
                {
                    if (!RouteParameterAccepts(routeSegment, selectorSegment))
                    {
                        return false;
                    }

                    continue;
                }

                if (selectorSegment == "*")
                {
                    continue;
                }

                if (selectorSegment.Contains('['))
                {
                    return false;
                }

                if (!string.Equals(routeSegment, selectorSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the routes the selector can reach, keeping only the most literal-specific
        /// matches: '/Employer/[Key]/Employees' matches both the Employees route and the
        /// '/Employer/{id}/{effectiveDate}' catch-all, but two literal segment matches beat one, so
        /// only the Employees route survives — mirroring how API routing resolves the request.
        /// </summary>
        private List<RouteDefinition> MatchRoutes(string[] selectorSegments)
        {
            var matches = this.GetRoutes()
                .Where(r => SegmentsMatch(r.Segments, selectorSegments))
                .Select(r => (r.Route, Score: LiteralMatchCount(r.Segments, selectorSegments)))
                .ToList();

            if (matches.Count == 0)
            {
                return new List<RouteDefinition>();
            }

            var bestScore = matches.Max(m => m.Score);

            return matches.Where(m => m.Score == bestScore).Select(m => m.Route).ToList();
        }

        /// <summary>
        /// Whether a route parameter slot can accept the selector segment. Variables and wildcards
        /// resolve at runtime so they always pass; literals must satisfy the parameter's type
        /// constraint when one is declared (<c>:int</c>, <c>:datetime(format)</c>).
        /// </summary>
        private static bool RouteParameterAccepts(string routeParameter, string selectorSegment)
        {
            if (selectorSegment == "*" || selectorSegment.Contains('['))
            {
                return true;
            }

            var inner = routeParameter.Trim('{', '}');
            var constraintIndex = inner.IndexOf(':');

            if (constraintIndex < 0)
            {
                return true;
            }

            var constraint = inner[(constraintIndex + 1)..];

            if (constraint.StartsWith("datetime", StringComparison.OrdinalIgnoreCase))
            {
                var openParen = constraint.IndexOf('(');
                var format = openParen >= 0 ? constraint[(openParen + 1)..].TrimEnd(')') : null;

                return format == null
                           ? DateTime.TryParse(selectorSegment, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)
                           : DateTime.TryParseExact(selectorSegment, format, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);
            }

            if (constraint.Equals("int", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(selectorSegment, out _);
            }

            // Unknown constraint kinds stay permissive — a false match is better than a false alarm.
            return true;
        }

        private static int LiteralMatchCount(string[] routeSegments, string[] selectorSegments)
        {
            var count = 0;

            for (var i = 0; i < routeSegments.Length; i++)
            {
                if (!routeSegments[i].StartsWith('{')
                    && string.Equals(routeSegments[i], selectorSegments[i], StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private IReadOnlyList<(RouteDefinition Route, string[] Segments)> GetRoutes()
        {
            if (this.getRoutes != null)
            {
                return this.getRoutes;
            }

            lock (this.syncLock)
            {
                this.getRoutes ??= this.repository
                    .GetRouteDefinitions()
                    .Where(r => string.Equals(r.Verb, "GET", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(r.RouteSignature))
                    .Select(r => (r, r.RouteSignature!.Trim('/').Split('/')))
                    .ToArray();
            }

            return this.getRoutes;
        }

        private HashSet<string> GetAllPropertyNames()
        {
            if (this.allPropertyNames != null)
            {
                return this.allPropertyNames;
            }

            lock (this.syncLock)
            {
                this.allPropertyNames ??= this.repository
                    .ListSchemas()
                    .SelectMany(s => s.Properties ?? Enumerable.Empty<Models.PropertyDefinition>())
                    .Select(p => p.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Cast<string>()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            return this.allPropertyNames;
        }

        private static string? TypeOf(XElement element) => element.Attribute(Xsi + "type")?.Value;

        private static ValidationDiagnostic Warn(XElement element, string code, string message)
        {
            var lineInfo = (IXmlLineInfo)element;

            return new ValidationDiagnostic
                {
                    Severity = ValidationSeverity.Warning,
                    Line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0,
                    Column = lineInfo.HasLineInfo() ? lineInfo.LinePosition : 0,
                    Code = code,
                    Message = message
                };
        }
    }
}
