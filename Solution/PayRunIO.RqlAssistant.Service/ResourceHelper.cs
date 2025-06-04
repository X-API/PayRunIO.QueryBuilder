namespace PayRunIO.RqlAssistant.Service
{
    using System.Reflection;

    public static class ResourceHelper
    {
        public const string FindSchemaNames = "find-schema-names-system-prompt.txt";

        public const string FindRouteNames = "find-route-names-system-prompt.txt";

        public const string AnswerQuestionSystemPrompt = "answer-question-system-prompt.txt";

        public const string TabularRql = "rql-tabular-queries.md";
        
        public const string RqlCheatSheet = "rql-cheat-sheet.md";
        
        public const string RqlDocJson = "rql-doc-json.md";

        public const string RqlDocXml = "rql-doc-xml.md";
        
        public const string QuerySchema = "QuerySchema.xsd";

        public const string Routes = "routes.json";

        public const string Dtos = "dtos.json";

        public static async Task<string> LoadResourceAsStringAsync(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceLocation = "PayRunIO.RqlAssistant.Service.Resources." + resourceName;

            using (var stream = assembly.GetManifestResourceStream(resourceLocation))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Resource '{resourceName}' not found in assembly.");
                }

                using (var reader = new StreamReader(stream))
                {
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
