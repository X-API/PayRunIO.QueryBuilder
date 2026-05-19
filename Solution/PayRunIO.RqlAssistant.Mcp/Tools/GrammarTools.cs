namespace PayRunIO.RqlAssistant.Mcp.Tools
{
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol.Server;

    using PayRunIO.RqlAssistant.Service;

    /// <summary>
    /// MCP tools exposing the RQL grammar documentation by topic. Backed by <see cref="IRqlGrammarIndex"/>.
    /// </summary>
    [McpServerToolType]
    public static class GrammarTools
    {
        [McpServerTool(Name = "list_rql_topics")]
        [Description("List every available RQL grammar topic that can be fetched with get_rql_syntax. Cheap to call — returns just slug + title for each topic. Use this to discover what's available before guessing topic names.")]
        public static IEnumerable<RqlGrammarTopicDto> ListRqlTopics(IRqlGrammarIndex index)
        {
            return index.Topics
                .Select(t => new RqlGrammarTopicDto { Slug = t.Slug, Title = t.Title })
                .ToArray();
        }

        [McpServerTool(Name = "get_rql_syntax")]
        [Description("Fetch a section of the RQL grammar documentation by topic slug. Returns the markdown for that section, including XML examples. Call list_rql_topics first if unsure which slug to use.")]
        public static RqlGrammarSectionDto? GetRqlSyntax(
            IRqlGrammarIndex index,
            [Description("The topic slug, e.g. 'filters', 'ordering', 'conditions-and-conditional-group-logic', 'outputs', 'variables', 'loop-expressions'. Case-insensitive.")] string topic)
        {
            var body = index.GetTopic(topic);

            return body == null
                ? null
                : new RqlGrammarSectionDto { Topic = topic, Content = body };
        }
    }

    public sealed class RqlGrammarTopicDto
    {
        [Description("The topic slug used as the key for get_rql_syntax.")]
        public string Slug { get; set; } = string.Empty;

        [Description("The human-readable section title from the source documentation.")]
        public string Title { get; set; } = string.Empty;
    }

    public sealed class RqlGrammarSectionDto
    {
        [Description("The topic slug that was fetched.")]
        public string Topic { get; set; } = string.Empty;

        [Description("The markdown body of the section, including any XML examples.")]
        public string Content { get; set; } = string.Empty;
    }
}
