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

        private readonly IDocumentRepository repository;

        private readonly object syncLock = new object();

        private IReadOnlyList<string[]>? routeSegments;

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
            CheckVariableAssignments(root, diagnostics);

            return diagnostics;
        }

        private void CheckSelectors(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            foreach (var group in root.Descendants("Group"))
            {
                var selector = group.Attribute("Selector")?.Value;

                if (string.IsNullOrWhiteSpace(selector))
                {
                    continue;
                }

                // A selector that is (or starts with) a variable can resolve to anything — unknowable statically.
                if (!selector.StartsWith('/'))
                {
                    continue;
                }

                var segments = selector.Trim('/').Split('/');

                if (this.GetRouteSegments().Any(route => SegmentsMatch(route, segments)))
                {
                    continue;
                }

                diagnostics.Add(Warn(
                    group,
                    "UnknownRoute",
                    $"Selector '{selector}' does not match any known GET API route. Use list_routes to find the correct URL template."));
            }
        }

        private void CheckPropertyReferences(XElement root, List<ValidationDiagnostic> diagnostics)
        {
            foreach (var group in root.Descendants("Group"))
            {
                // OfType filters pin the entity type(s) in scope; property checks become exact.
                var ofTypeSchemas = group.Elements("Filter")
                    .Where(f => string.Equals(TypeOf(f), "OfType", StringComparison.OrdinalIgnoreCase))
                    .Select(f => f.Attribute("Value")?.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Cast<string>()
                    .ToArray();

                var scopedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var scopeKnown = ofTypeSchemas.Length > 0;

                foreach (var typeName in ofTypeSchemas)
                {
                    var schema = this.repository.GetSchema(typeName);

                    if (schema == null)
                    {
                        diagnostics.Add(Warn(
                            group,
                            "UnknownEntityType",
                            $"OfType filter names '{typeName}' which matches no known entity schema. Use list_schemas to find the correct type name."));
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

                foreach (var element in group.Elements().Where(e => e.Name.LocalName is "Output" or "Filter" or "Order"))
                {
                    var property = element.Attribute("Property")?.Value;

                    // Skip absent, variable-substituted and dotted-path references.
                    if (string.IsNullOrWhiteSpace(property) || property.Contains('[') || property.Contains('.'))
                    {
                        continue;
                    }

                    if (scopeKnown)
                    {
                        if (!scopedProperties.Contains(property))
                        {
                            diagnostics.Add(Warn(
                                element,
                                "UnknownProperty",
                                $"Property '{property}' does not exist on {string.Join("/", ofTypeSchemas)}. Use get_schema to confirm property names."));
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
                    var name = output.Attribute("Name")?.Value;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        assigned.Add(name);
                    }

                    // RenderLink variable output writes to the reserved [Link] variable.
                    if (string.Equals(TypeOf(output), "RenderLink", StringComparison.OrdinalIgnoreCase))
                    {
                        assigned.Add("[Link]");
                    }
                }
            }

            if (root.Descendants("Group").Any(g => g.Attribute("LoopExpression") != null))
            {
                assigned.Add("[LoopVariable]");
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
        /// A selector matches a route when segment counts are equal and each pair matches:
        /// route parameters (<c>{...}</c>) accept anything; selector variables (<c>[Var]</c>) and
        /// wildcards (<c>*</c>) accept any route segment; literals must match case-insensitively.
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
                    continue;
                }

                if (selectorSegment == "*" || selectorSegment.Contains('['))
                {
                    continue;
                }

                if (!string.Equals(routeSegment, selectorSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private IReadOnlyList<string[]> GetRouteSegments()
        {
            if (this.routeSegments != null)
            {
                return this.routeSegments;
            }

            lock (this.syncLock)
            {
                this.routeSegments ??= this.repository
                    .GetRouteDefinitions()
                    .Where(r => string.Equals(r.Verb, "GET", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(r.RouteSignature))
                    .Select(r => r.RouteSignature!.Trim('/').Split('/'))
                    .ToArray();
            }

            return this.routeSegments;
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
