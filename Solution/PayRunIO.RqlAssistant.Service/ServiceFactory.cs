namespace PayRunIO.RqlAssistant.Service
{
    using Microsoft.Extensions.Configuration;

    public static class ServiceFactory
    {
        private const string DefaultTimeoutAsString = "00:05:00";

        public static IRqlRagService CreateService(IConfiguration configuration, HttpClient? httpClient = null)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var remoteAiService =
                new RemoteAiService(
                    configuration,
                    httpClient ?? new HttpClient
                        {
                            Timeout = TimeSpan.Parse(configuration["OpenAi:HttpClient:TimeOut"] ?? DefaultTimeoutAsString)
                        });

            var requestBuilderService = new RequestBuilderService(configuration);
            var documentRepository = new DocumentRepository();
            var queryValidator = new QueryValidator();
            var grammarIndex = new RqlGrammarIndex();
            var exampleIndex = new RqlExampleIndex();
            var semanticLinter = new RqlSemanticLinter(documentRepository);
            var toolDispatcher = new RqlToolDispatcher(documentRepository, queryValidator, grammarIndex, exampleIndex, semanticLinter);

            return new RqlRagService(requestBuilderService, remoteAiService, toolDispatcher);
        }

        /// <summary>
        /// Creates a standalone <see cref="IQueryValidator"/> instance. Useful when a caller (e.g. the WPF
        /// assistant window) wants to validate the model's final XML reply directly, independent of the
        /// tool-call loop, to drive its own retry.
        /// </summary>
        public static IQueryValidator CreateValidator() => new QueryValidator();
    }
}
