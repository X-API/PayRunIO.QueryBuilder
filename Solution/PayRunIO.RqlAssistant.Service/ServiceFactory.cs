namespace PayRunIO.RqlAssistant.Service
{
    using Microsoft.Extensions.Configuration;

    public static class ServiceFactory
    {
        private const string DefaultTimeoutAsString = "00:05:00";

        public static IRqlRagService CreateService(IConfiguration configuration)
        {
            var remoteAiService = 
                new RemoteAiService(
                    configuration, 
                    new HttpClient
                        {
                            Timeout = TimeSpan.Parse(configuration["HttpClient:TimeOut"] ?? DefaultTimeoutAsString)
                        });
            var requestBuilderService = new RequestBuilderService(configuration);
            var documentRepository = new DocumentRepository();

            var rqlRagService = new RqlRagService(requestBuilderService, documentRepository, remoteAiService);

            return rqlRagService;
        }
    }
}
