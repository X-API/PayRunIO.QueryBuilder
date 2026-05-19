namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public interface IRqlGrammarIndex
    {
        IReadOnlyList<RqlGrammarTopic> Topics { get; }

        string? GetTopic(string slug);
    }

    public sealed record RqlGrammarTopic(string Slug, string Title);

    /// <summary>
    /// Indexes <c>rql-doc-xml.md</c> by its <c>## </c> headings so individual topics can be retrieved by slug.
    /// This is the read-side of the [[feedback-rag-pitfalls]] strategy: the model fetches grammar on demand
    /// rather than carrying the full 126 KB document in every prompt.
    /// </summary>
    public sealed class RqlGrammarIndex : IRqlGrammarIndex
    {
        private static readonly Regex SectionHeading = new Regex(@"^##\s+(?<title>.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly HashSet<string> ExcludedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "table-of-contents"
            };

        private readonly object syncLock = new object();

        private IReadOnlyList<RqlGrammarTopic>? topics;

        private Dictionary<string, string>? bodies;

        public IReadOnlyList<RqlGrammarTopic> Topics
        {
            get
            {
                this.EnsureLoaded();
                return this.topics!;
            }
        }

        public string? GetTopic(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            this.EnsureLoaded();

            return this.bodies!.TryGetValue(slug, out var body) ? body : null;
        }

        private void EnsureLoaded()
        {
            if (this.bodies != null)
            {
                return;
            }

            lock (this.syncLock)
            {
                if (this.bodies != null)
                {
                    return;
                }

                var markdown = ResourceHelper
                    .LoadResourceAsStringAsync(ResourceHelper.RqlDocXml)
                    .GetAwaiter()
                    .GetResult();

                var (topicList, bodyMap) = Parse(markdown);
                this.topics = topicList;
                this.bodies = bodyMap;
            }
        }

        public static (IReadOnlyList<RqlGrammarTopic> Topics, Dictionary<string, string> Bodies) Parse(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return (Array.Empty<RqlGrammarTopic>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }

            var matches = SectionHeading.Matches(markdown);

            var topicList = new List<RqlGrammarTopic>(matches.Count);
            var bodyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var title = match.Groups["title"].Value.Trim();
                var slug = Slugify(title);

                if (ExcludedSlugs.Contains(slug))
                {
                    continue;
                }

                var bodyStart = match.Index;
                var bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;
                var body = markdown.Substring(bodyStart, bodyEnd - bodyStart).TrimEnd();

                topicList.Add(new RqlGrammarTopic(slug, title));
                bodyMap[slug] = body;
            }

            return (topicList, bodyMap);
        }

        /// <summary>
        /// Slugify: lowercase, drop everything that isn't ASCII alphanumeric or whitespace, collapse runs
        /// of whitespace to single hyphens. Matches the markdown anchor convention used in the source doc.
        /// </summary>
        public static string Slugify(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(title.Length);
            var lastWasHyphen = false;

            foreach (var c in title)
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasHyphen = false;
                }
                else if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                {
                    if (!lastWasHyphen && sb.Length > 0)
                    {
                        sb.Append('-');
                        lastWasHyphen = true;
                    }
                }
                // All other characters (punctuation, emoji, etc.) dropped silently.
            }

            return sb.ToString().Trim('-');
        }
    }
}
