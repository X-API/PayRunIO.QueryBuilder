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
        [Description(RqlToolDescriptions.ListRqlTopics)]
        public static IEnumerable<RqlGrammarTopicDto> ListRqlTopics(IRqlGrammarIndex index)
        {
            return index.Topics
                .Select(t => new RqlGrammarTopicDto { Slug = t.Slug, Title = t.Title })
                .ToArray();
        }

        [McpServerTool(Name = "get_rql_syntax")]
        [Description(RqlToolDescriptions.GetRqlSyntax)]
        public static RqlGrammarSectionDto? GetRqlSyntax(
            IRqlGrammarIndex index,
            [Description(RqlToolDescriptions.GetRqlSyntaxTopicParam)] string topic)
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
