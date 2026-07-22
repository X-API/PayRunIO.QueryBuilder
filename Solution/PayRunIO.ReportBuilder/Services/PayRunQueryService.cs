namespace PayRunIO.ReportBuilder.Services
{
    using System.Net.Http.Headers;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    using PayRunIO.ReportBuilder.Auth;
    using PayRunIO.ReportBuilder.Logging;

    /// <summary>
    /// Executes RQL queries against the configured PayRun.io API instance using the signed in
    /// user's KeyCloak access token (POST /Query, XML in/out).
    /// </summary>
    public sealed class PayRunQueryService
    {
        public const string HttpClientName = "PayRunApi";

        private readonly IHttpClientFactory httpClientFactory;

        private readonly ApiTokenAccessor apiTokenAccessor;

        private readonly QueryFailureLog failureLog;

        public PayRunQueryService(
            IHttpClientFactory httpClientFactory,
            ApiTokenAccessor apiTokenAccessor,
            QueryFailureLog failureLog)
        {
            this.httpClientFactory = httpClientFactory;
            this.apiTokenAccessor = apiTokenAccessor;
            this.failureLog = failureLog;
        }

        /// <summary>
        /// Executes a report query against the PayRun.io API.
        /// </summary>
        /// <param name="queryXml">The RQL query to execute.</param>
        /// <param name="origin">
        /// Where the query came from. Recorded against any failure so assistant generated queries
        /// can be told apart from hand written ones when reviewing the failure log.
        /// </param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task<QueryExecutionResult> ExecuteQueryAsync(
            string queryXml,
            QueryOrigin origin = QueryOrigin.Unknown,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(queryXml))
            {
                throw new ArgumentException("Query XML cannot be empty.", nameof(queryXml));
            }

            var accessToken = await this.apiTokenAccessor.GetAccessTokenAsync(cancellationToken);

            var httpClient = this.httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/Query");

            // The PayRun.io API rejects content type parameters (e.g. "; charset=utf-8"), so the
            // default StringContent header must be replaced with the bare media type.
            request.Content = new StringContent(queryXml, Encoding.UTF8, "application/xml");
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            HttpResponseMessage response;

            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Transport level faults never reach the caller's result object, so record them
                // here rather than relying on the page's catch all.
                this.failureLog.QueryFaulted(queryXml, exception, origin);
                throw;
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = ExtractErrorMessage(body);

                    this.failureLog.QueryRejected(
                        queryXml,
                        (int)response.StatusCode,
                        response.ReasonPhrase,
                        errorMessage,
                        origin);

                    return new QueryExecutionResult
                        {
                            Success = false,
                            ErrorMessage = $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorMessage}",
                            RawXml = TryBeautify(body),
                        };
                }

                XDocument document;

                try
                {
                    document = XDocument.Parse(body);
                }
                catch (XmlException exception)
                {
                    // A 2xx response that is not XML: the report still renders, but something in the
                    // API contract has moved, which is worth surfacing.
                    this.failureLog.QueryFaulted(queryXml, exception, origin);

                    return new QueryExecutionResult { Success = true, RawXml = body };
                }

                var table = TabularResult.TryParse(document);

                if (table == null)
                {
                    this.failureLog.ResponseNotTabular(queryXml, origin);
                }

                return new QueryExecutionResult
                    {
                        Success = true,
                        RawXml = document.ToString(),
                        Table = table,
                    };
            }
        }

        internal static string ExtractErrorMessage(string body)
        {
            try
            {
                var document = XDocument.Parse(body);

                var message = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Message")?.Value;

                var errors = document
                    .Descendants()
                    .Where(e => e.Name.LocalName == "Errors")
                    .SelectMany(e => e.Elements())
                    .Select(e => e.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();

                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(message))
                {
                    parts.Add(message);
                }

                parts.AddRange(errors);

                return parts.Count > 0 ? string.Join(" | ", parts) : body;
            }
            catch (XmlException)
            {
                return body;
            }
        }

        private static string TryBeautify(string xml)
        {
            try
            {
                return XDocument.Parse(xml).ToString();
            }
            catch (XmlException)
            {
                return xml;
            }
        }
    }
}
