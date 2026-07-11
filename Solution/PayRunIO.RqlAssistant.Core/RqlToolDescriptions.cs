namespace PayRunIO.RqlAssistant.Service
{
    /// <summary>
    /// Single source of truth for tool and parameter descriptions shared by the in-process
    /// tool surface (<see cref="RqlToolDispatcher"/> descriptors) and the MCP server's
    /// <c>[Description]</c> attributes. Attributes require compile-time constants, hence
    /// <c>const string</c> rather than a resource. Edit here; both surfaces update together.
    /// </summary>
    public static class RqlToolDescriptions
    {
        public const string ListSchemas =
            "List all PayRunIO entity schemas. Returns name and description only — call get_schema for full property details. Optionally filter by a case-insensitive substring match on the schema name.";

        public const string ListSchemasFilterParam =
            "Optional case-insensitive substring filter applied to schema names. Omit to list all schemas.";

        public const string GetSchema =
            "Get the full definition of a single PayRunIO entity schema, including all of its properties. Use this to ground RQL queries against the real shape of entities like Employee, EmployeeSummary, PayRun, etc. Match is exact and case-insensitive; returns null if the name is unknown.";

        public const string GetSchemaTypeNameParam =
            "The exact schema type name, e.g. 'Employee', 'EmployeeSummary', 'PayRun'. Case-insensitive.";

        public const string ListRoutes =
            "List PayRunIO API routes. Returns class name, verb, URL template and a short summary — call get_route for the full description and response type. Filters are optional and ANDed together.";

        public const string ListRoutesFilterParam =
            "Optional case-insensitive substring filter applied to the route URL template (RouteSignature). E.g. 'Employee' matches '/Employer/{employerId}/Employee/{employeeId}'.";

        public const string ListRoutesVerbParam =
            "Optional HTTP verb filter, case-insensitive exact match. E.g. 'GET', 'POST', 'PUT', 'DELETE', 'PATCH'.";

        public const string ListRoutesTagParam =
            "Optional tag filter, case-insensitive exact match against any tag on the route. E.g. 'Employee', 'PayRun', 'Reports'.";

        public const string GetRoute =
            "Get the full definition of a single PayRunIO API route by its class name (the unique key returned by list_routes). Match is exact and case-insensitive; returns null if the class name is unknown.";

        public const string GetRouteClassNameParam =
            "The exact route class name, e.g. 'GetEmployeeRoute', 'GetAEAssessmentFromEmployeeRoute'. Case-insensitive.";

        public const string ValidateQuery =
            "Validate a candidate RQL <Query> XML document against the PayRunIO QuerySchema.xsd, plus semantic lint checks: selectors that match no known GET API route (variables like [Key] only substitute into route parameter slots), property names that do not exist on the entity type the group selects, variables used but never assigned, Order/Filter elements in entity-less groups (silently ignored), and per-entity renders over collection selectors inside Table query rows (column misalignment). Returns structured diagnostics (line, column, code, message) so a caller can fix the query and retry. IsValid is true only when no Error-level diagnostics are produced; Warnings (including all lint findings) do not invalidate the query but almost always indicate a real mistake — resolve them all before finalising.";

        public const string ValidateQueryXmlParam =
            "The full RQL query XML to validate, starting at the <Query> root element.";

        public const string ListRqlTopics =
            "List every available RQL grammar topic that can be fetched with get_rql_syntax. Cheap to call — returns just slug + title for each topic. Use this to discover what's available before guessing topic names.";

        public const string GetRqlSyntax =
            "Fetch a section of the RQL grammar documentation by topic slug. Returns the markdown for that section, including XML examples. Call list_rql_topics first if unsure which slug to use. Topics cover constructs like filters, ordering, conditions, outputs, variables, loop expressions, advanced features, etc.";

        public const string GetRqlSyntaxTopicParam =
            "The topic slug, e.g. 'filters', 'ordering', 'conditions-and-conditional-group-logic', 'outputs', 'variables', 'loop-expressions'. Case-insensitive.";

        public const string ListExamples =
            "List the curated bank of validated RQL example queries. Returns slug, title, the natural-language request each example answers, and tags. Before composing RQL from scratch, check here for a close match and adapt it with get_example — adapting a validated example is more reliable than free composition.";

        public const string ListExamplesFilterParam =
            "Optional case-insensitive keyword filter matched against slug, title, request and tags. Multiple space-separated terms are ANDed, e.g. 'pension employee'. Omit to list all examples.";

        public const string GetExample =
            "Fetch a single RQL example by its slug (from list_examples). Returns the full markdown section: the request it answers, an explanation, the complete validated <Query> XML, and notes on adapting it.";

        public const string GetExampleSlugParam =
            "The example slug, e.g. 'net-pay-per-employee-for-a-payment-date', 'tabular-gross-to-net-report'. Case-insensitive.";
    }
}
