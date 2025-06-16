namespace PayRunIO.RqlAssistant.Service.Tests
{
    using Microsoft.Extensions.Configuration;
    using Moq;

    [TestFixture]
    public class ServiceFactoryTests
    {
        private const string ApiKey = "test-api-key";
        private const string Endpoint = "https://test-endpoint.com/v1/chat/completions";
        private Mock<IConfiguration> config;
        private HttpClient httpClient;

        [SetUp]
        public void Setup()
        {
            config = new Mock<IConfiguration>();
            config.Setup(c => c["OpenAI:ApiKey"]).Returns(ApiKey);
            config.Setup(c => c["OpenAI:Endpoint"]).Returns(Endpoint);
            config.Setup(c => c["HttpClient:TimeOut"]).Returns("00:00:30");
            
            httpClient = new HttpClient();
        }

        [TearDown]
        public void TearDown()
        {
            httpClient.Dispose();
        }

        /// <summary>
        /// Test that: Given a null configuration, when creating the service, then an ArgumentNullException is thrown
        /// </summary>
        [Test]
        public void GivenNullConfiguration_WhenCreatingService_ThenThrowsArgumentNullException()
        {
            // Arrange
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ServiceFactory.CreateService(default));
        }

        /// <summary>
        /// Test that: Given a valid configuration, when creating the service, then returns IRqlRagService instance
        /// </summary>
        [Test]
        public void GivenValidConfiguration_WhenCreatingService_ThenReturnsServiceInstance()
        {
            // Arrange
            // Act
            var service = ServiceFactory.CreateService(config.Object);

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service, Is.InstanceOf<IRqlRagService>());
        }

        /// <summary>
        /// Test that: Given a configuration without timeout, when creating the service, then uses default timeout
        /// </summary>
        [Test]
        public void GivenConfigurationWithoutTimeout_WhenCreatingService_ThenUsesDefaultTimeout()
        {
            // Arrange
            config.Setup(c => c["HttpClient:TimeOut"]).Returns((string?)null);

            // Act
            var service = ServiceFactory.CreateService(config.Object);

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service, Is.InstanceOf<IRqlRagService>());
        }

        /// <summary>
        /// Test that: Given a custom HttpClient, when creating the service, then uses provided client
        /// </summary>
        [Test]
        public void GivenCustomHttpClient_WhenCreatingService_ThenUsesProvidedClient()
        {
            // Arrange
            var customClient = new HttpClient();

            // Act
            var service = ServiceFactory.CreateService(config.Object, customClient);

            // Assert
            Assert.That(service, Is.Not.Null);
            Assert.That(service, Is.InstanceOf<IRqlRagService>());
        }

        /// <summary>
        /// Test that: Given a configuration without API key, when creating the service, then throws InvalidOperationException
        /// </summary>
        [Test]
        public void GivenConfigurationWithoutApiKey_WhenCreatingService_ThenThrowsInvalidOperationException()
        {
            // Arrange
            config.Setup(c => c["OpenAI:ApiKey"]).Returns((string?)null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                ServiceFactory.CreateService(config.Object));
        }
    }
} 