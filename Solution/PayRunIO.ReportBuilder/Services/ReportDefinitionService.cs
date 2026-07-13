namespace PayRunIO.ReportBuilder.Services
{
    using System.Net.Http.Headers;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    using PayRunIO.ReportBuilder.Auth;

    /// <summary>Summary of a report definition managed by the report builder (prefix stripped).</summary>
    public sealed record SavedReportSummary(string Id, string Name);

    /// <summary>A managed report definition loaded from the API, with the query unwrapped back into
    /// standalone Query XML for the designer.</summary>
    public sealed record SavedReport(string Id, string Name, string QueryXml);

    /// <summary>Raised when a report definition API call fails; the message is safe to show in the UI.</summary>
    public sealed class ReportPersistenceException : Exception
    {
        public ReportPersistenceException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Persists report builder queries as PayRun.io report definitions, using the signed in user's
    /// access token so API permissions apply. Managed definitions are identified by the
    /// "ReportBuilder-" title prefix; the designer only ever edits the wrapped query — this service
    /// owns the wrapping and unwrapping of the ReportDefinition envelope.
    /// </summary>
    public sealed class ReportDefinitionService
    {
        public const string ManagedReportPrefix = "ReportBuilder-";

        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        private readonly IHttpClientFactory httpClientFactory;

        private readonly ApiTokenAccessor apiTokenAccessor;

        public ReportDefinitionService(IHttpClientFactory httpClientFactory, ApiTokenAccessor apiTokenAccessor)
        {
            this.httpClientFactory = httpClientFactory;
            this.apiTokenAccessor = apiTokenAccessor;
        }

        /// <summary>Lists the report definitions whose title carries the managed prefix.</summary>
        public async Task<IReadOnlyList<SavedReportSummary>> ListManagedReportsAsync(CancellationToken cancellationToken = default)
        {
            var document = await this.SendAsync(HttpMethod.Get, "/Reports", null, cancellationToken);

            return document
                .Descendants()
                .Where(e => e.Name.LocalName == "Link")
                .Select(link => new
                    {
                        Title = ReadLinkValue(link, "Title"),
                        Href = ReadLinkValue(link, "Href"),
                    })
                .Where(link => !string.IsNullOrEmpty(link.Title)
                               && !string.IsNullOrEmpty(link.Href)
                               && link.Title.StartsWith(ManagedReportPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(link => new SavedReportSummary(IdFromHref(link.Href!), link.Title![ManagedReportPrefix.Length..]))
                .OrderBy(report => report.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<SavedReport> LoadReportAsync(string reportId, CancellationToken cancellationToken = default)
        {
            var document = await this.SendAsync(
                HttpMethod.Get,
                $"/Report/{Uri.EscapeDataString(reportId)}",
                null,
                cancellationToken);

            var definition = document.Root;

            var reportQuery = definition?.Elements().FirstOrDefault(e => e.Name.LocalName == "ReportQuery");

            if (definition == null || reportQuery == null)
            {
                throw new ReportPersistenceException("The report definition did not contain a report query.");
            }

            var title = definition.Elements().FirstOrDefault(e => e.Name.LocalName == "Title")?.Value ?? reportId;

            var name = title.StartsWith(ManagedReportPrefix, StringComparison.OrdinalIgnoreCase)
                           ? title[ManagedReportPrefix.Length..]
                           : title;

            // Re-root the ReportQuery element as a standalone <Query> document for the designer.
            var query = new XElement("Query", reportQuery.Attributes(), reportQuery.Nodes());

            if (query.Attribute(XNamespace.Xmlns + "xsi") == null)
            {
                query.Add(new XAttribute(XNamespace.Xmlns + "xsi", Xsi));
            }

            return new SavedReport(reportId, name, query.ToString());
        }

        /// <summary>Creates a new managed report definition and returns its API identifier.</summary>
        public async Task<string> CreateReportAsync(string name, string queryXml, CancellationToken cancellationToken = default)
        {
            var body = BuildReportDefinitionXml(name, queryXml);

            var document = await this.SendAsync(HttpMethod.Post, "/Reports", body, cancellationToken);

            var href = document
                .Descendants()
                .Where(e => e.Name.LocalName == "Link")
                .Select(link => ReadLinkValue(link, "Href"))
                .FirstOrDefault(value => !string.IsNullOrEmpty(value));

            if (string.IsNullOrEmpty(href))
            {
                throw new ReportPersistenceException("The report was created but the API response did not include its location.");
            }

            return IdFromHref(href);
        }

        public async Task UpdateReportAsync(string reportId, string name, string queryXml, CancellationToken cancellationToken = default)
        {
            var body = BuildReportDefinitionXml(name, queryXml);

            await this.SendAsync(HttpMethod.Put, $"/Report/{Uri.EscapeDataString(reportId)}", body, cancellationToken);
        }

        private static string BuildReportDefinitionXml(string name, string queryXml)
        {
            XElement query;

            try
            {
                query = XElement.Parse(queryXml);
            }
            catch (XmlException exception)
            {
                throw new ReportPersistenceException($"The report query is not valid XML: {exception.Message}");
            }

            // The definition wraps the designer's <Query> as its <ReportQuery> child element; the xsi
            // namespace declaration moves to the definition root so xsi:type attributes stay valid.
            var definition = new XElement(
                "ReportDefinition",
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XElement("Title", ManagedReportPrefix + name),
                new XElement("Readonly", "false"),
                new XElement("Active", "true"),
                new XElement(
                    "ReportQuery",
                    query.Attributes().Where(a => !a.IsNamespaceDeclaration),
                    query.Nodes()));

            return definition.ToString();
        }

        // The live API serialises link attributes in lowercase (title/href/rel), so match names
        // case-insensitively, and accept child elements as a fallback.
        private static string? ReadLinkValue(XElement link, string name) =>
            link.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value
            ?? link.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

        private static string IdFromHref(string href) =>
            href.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Last();

        private async Task<XDocument> SendAsync(
            HttpMethod method,
            string requestUri,
            string? xmlBody,
            CancellationToken cancellationToken)
        {
            var accessToken = await this.apiTokenAccessor.GetAccessTokenAsync(cancellationToken);

            var httpClient = this.httpClientFactory.CreateClient(PayRunQueryService.HttpClientName);

            using var request = new HttpRequestMessage(method, requestUri);

            if (xmlBody != null)
            {
                // As in PayRunQueryService: the API rejects content type parameters, so the default
                // "; charset=utf-8" suffix must be stripped from the header.
                request.Content = new StringContent(xmlBody, Encoding.UTF8, "application/xml");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            using var response = await httpClient.SendAsync(request, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new ReportPersistenceException(
                    $"{(int)response.StatusCode} {response.ReasonPhrase}: {PayRunQueryService.ExtractErrorMessage(body)}");
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return new XDocument();
            }

            try
            {
                return XDocument.Parse(body);
            }
            catch (XmlException)
            {
                return new XDocument();
            }
        }
    }
}
