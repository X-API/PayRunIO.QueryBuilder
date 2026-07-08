namespace PayRunIO.RqlAssistant.Mcp.Tools
{
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol.Server;

    using PayRunIO.RqlAssistant.Service;

    /// <summary>
    /// MCP tools exposing the curated RQL example bank. Backed by <see cref="IRqlExampleIndex"/>.
    /// </summary>
    [McpServerToolType]
    public static class ExampleTools
    {
        [McpServerTool(Name = "list_examples")]
        [Description(RqlToolDescriptions.ListExamples)]
        public static IEnumerable<RqlExampleSummaryDto> ListExamples(
            IRqlExampleIndex index,
            [Description(RqlToolDescriptions.ListExamplesFilterParam)] string? filter = null)
        {
            return RqlToolDispatcher.FilterExamples(index.Examples, filter)
                .Select(e => new RqlExampleSummaryDto
                    {
                        Slug = e.Slug,
                        Title = e.Title,
                        Request = e.Request,
                        Tags = e.Tags.ToArray()
                    })
                .ToArray();
        }

        [McpServerTool(Name = "get_example")]
        [Description(RqlToolDescriptions.GetExample)]
        public static RqlExampleDto? GetExample(
            IRqlExampleIndex index,
            [Description(RqlToolDescriptions.GetExampleSlugParam)] string slug)
        {
            var example = index.GetExample(slug);

            return example == null
                ? null
                : new RqlExampleDto { Slug = example.Slug, Content = example.Body };
        }
    }

    public sealed class RqlExampleSummaryDto
    {
        [Description("The example slug used as the key for get_example.")]
        public string Slug { get; set; } = string.Empty;

        [Description("The human-readable example title.")]
        public string Title { get; set; } = string.Empty;

        [Description("The natural-language request this example answers.")]
        public string Request { get; set; } = string.Empty;

        [Description("Tags describing the entities and RQL constructs the example uses.")]
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    public sealed class RqlExampleDto
    {
        [Description("The example slug that was fetched.")]
        public string Slug { get; set; } = string.Empty;

        [Description("The markdown body of the example, including the validated <Query> XML and adaptation notes.")]
        public string Content { get; set; } = string.Empty;
    }
}
