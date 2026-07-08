namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public interface IRqlExampleIndex
    {
        IReadOnlyList<RqlExample> Examples { get; }

        RqlExample? GetExample(string slug);
    }

    /// <summary>
    /// A curated example query from <c>rql-examples.md</c>. <see cref="Body"/> is the full markdown
    /// section including the request, explanation, query XML and notes.
    /// </summary>
    public sealed record RqlExample(string Slug, string Title, string Request, IReadOnlyList<string> Tags, string Body);

    /// <summary>
    /// Indexes <c>rql-examples.md</c> by its <c>## </c> headings so examples can be listed by
    /// tag/keyword and fetched by slug. Companion to <see cref="RqlGrammarIndex"/>: grammar topics
    /// say what is legal, examples say what is idiomatic.
    /// </summary>
    public sealed class RqlExampleIndex : IRqlExampleIndex
    {
        private static readonly Regex SectionHeading = new Regex(@"^##\s+(?<title>.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex RequestLine = new Regex(@"^\s*-\s+\*\*Request:\*\*\s*(?<value>.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex TagsLine = new Regex(@"^\s*-\s+\*\*Tags:\*\*\s*(?<value>.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        private readonly object syncLock = new object();

        private IReadOnlyList<RqlExample>? examples;

        private Dictionary<string, RqlExample>? bySlug;

        public IReadOnlyList<RqlExample> Examples
        {
            get
            {
                this.EnsureLoaded();
                return this.examples!;
            }
        }

        public RqlExample? GetExample(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            this.EnsureLoaded();

            return this.bySlug!.TryGetValue(slug, out var example) ? example : null;
        }

        private void EnsureLoaded()
        {
            if (this.examples != null)
            {
                return;
            }

            lock (this.syncLock)
            {
                if (this.examples != null)
                {
                    return;
                }

                var markdown = ResourceHelper
                    .LoadResourceAsStringAsync(ResourceHelper.RqlExamples)
                    .GetAwaiter()
                    .GetResult();

                var parsed = Parse(markdown);
                this.bySlug = parsed.ToDictionary(e => e.Slug, StringComparer.OrdinalIgnoreCase);
                this.examples = parsed;
            }
        }

        public static IReadOnlyList<RqlExample> Parse(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return Array.Empty<RqlExample>();
            }

            var matches = SectionHeading.Matches(markdown);
            var result = new List<RqlExample>(matches.Count);

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var title = match.Groups["title"].Value.Trim();
                var slug = RqlGrammarIndex.Slugify(title);

                var bodyStart = match.Index;
                var bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;
                var body = markdown.Substring(bodyStart, bodyEnd - bodyStart).TrimEnd();

                var requestMatch = RequestLine.Match(body);
                var tagsMatch = TagsLine.Match(body);

                var tags = tagsMatch.Success
                               ? tagsMatch.Groups["value"].Value
                                   .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                   .ToArray()
                               : Array.Empty<string>();

                result.Add(new RqlExample(
                    slug,
                    title,
                    requestMatch.Success ? requestMatch.Groups["value"].Value : string.Empty,
                    tags,
                    body));
            }

            return result;
        }
    }
}
