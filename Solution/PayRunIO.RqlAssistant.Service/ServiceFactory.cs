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

            var rqlRagService = new RqlRagService(requestBuilderService, documentRepository, remoteAiService);

            return rqlRagService;
        }
    }
}
