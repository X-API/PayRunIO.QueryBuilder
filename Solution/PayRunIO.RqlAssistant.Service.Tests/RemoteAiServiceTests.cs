namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Net;

    using Microsoft.Extensions.Configuration;

    using Moq;
    using Moq.Protected;

    [TestFixture]
    public class RemoteAiServiceTests
    {
        private const string ApiKey = "test-api-key";
        private const string Endpoint = "https://test-endpoint.com/v1/chat/completions";
        private Mock<IConfiguration> config;
        private Mock<HttpMessageHandler> handler;
        private HttpClient httpClient;

        [SetUp]
        public void Setup()
        {
            config = new Mock<IConfiguration>();
            config.Setup(c => c["OpenAI:ApiKey"]).Returns(ApiKey);
            config.Setup(c => c["OpenAI:Endpoint"]).Returns(Endpoint);
            config.Setup(c => c["HttpClient:TimeOut"]).Returns("00:00:30");

            handler = new Mock<HttpMessageHandler>();
            httpClient = new HttpClient(handler.Object);
        }

        [TearDown]
        public void TearDown()
        {
            httpClient.Dispose();
        }

        private void SetupHttpResponse(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => responseFactory(request));
        }

        /// <summary>
        /// Test that: Given a null configuration, when creating the service, then an ArgumentNullException is thrown
        /// </summary>
        [Test]
        public void GivenNullConfiguration_WhenCreatingService_ThenThrowsArgumentNullException()
        {
            // Arrange
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                ServiceFactory.CreateService(null));
        }

        /// <summary>
        /// Test that: Given a configuration without an API key, when creating the service, then an InvalidOperationException is thrown
        /// </summary>
        [Test]
        public void GivenConfigurationWithoutApiKey_WhenCreatingService_ThenThrowsInvalidOperationException()
        {
            // Arrange
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["OpenAI:ApiKey"]).Returns((string)null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                ServiceFactory.CreateService(config.Object));
        }

        /// <summary>
        /// Test that: Given a valid configuration and successful response, when asking a question, then the response content is returned
        /// </summary>
        [Test]
        public async Task GivenValidConfigurationAndSuccessfulResponse_WhenAskingQuestion_ThenReturnsResponseContent()
        {
            // Arrange
            var responseJson = "{\"choices\":[{\"message\":{\"content\":\"Hello!\"}}]}";
            SetupHttpResponse(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });

            var service = ServiceFactory.CreateService(config.Object, httpClient);

            // Act
            var result = await service.AskQuestion("test question");

            // Assert
            Assert.That(result, Is.EqualTo("Hello!"));
        }

        /// <summary>
        /// Test that: Given a null or empty question, when asking a question, then an ArgumentException is thrown
        /// </summary>
        [Test]
        public void GivenNullOrEmptyQuestion_WhenAskingQuestion_ThenThrowsArgumentException()
        {
            // Arrange
            var service = ServiceFactory.CreateService(config.Object, httpClient);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(() => service.AskQuestion(null));
            Assert.ThrowsAsync<ArgumentException>(() => service.AskQuestion("   "));
        }

        /// <summary>
        /// Test that: Given an error response from the API, when asking a question, then an OpenAiException is thrown with the error message
        /// </summary>
        [Test]
        public void GivenErrorResponse_WhenAskingQuestion_ThenThrowsOpenAiExceptionWithErrorMessage()
        {
            // Arrange
            var errorJson = "{\"error\":{\"message\":\"Test error\"}}";
            SetupHttpResponse(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorJson),
                ReasonPhrase = "Bad Request"
            });

            var service = ServiceFactory.CreateService(config.Object, httpClient);

            // Act & Assert
            var ex = Assert.ThrowsAsync<OpenAiException>(async () =>
                await service.AskQuestion("test question"));
            Assert.That(ex.Message, Is.EqualTo("Test error"));
            Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        /// <summary>
        /// Test that: Given a malformed JSON response, when asking a question, then an OpenAiException is thrown
        /// </summary>
        [Test]
        public void GivenMalformedJsonResponse_WhenAskingQuestion_ThenThrowsOpenAiException()
        {
            // Arrange
            SetupHttpResponse(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not json")
            });

            var service = ServiceFactory.CreateService(config.Object, httpClient);

            // Act & Assert
            var ex = Assert.ThrowsAsync<OpenAiException>(async () =>
                await service.AskQuestion("test question"));
            Assert.That(ex.Message, Does.Contain("Failed to parse OpenAI response JSON"));
        }

        /// <summary>
        /// Test that: Given a response without choices, when asking a question, then an OpenAiException is thrown
        /// </summary>
        [Test]
        public void GivenResponseWithoutChoices_WhenAskingQuestion_ThenThrowsOpenAiException()
        {
            // Arrange
            SetupHttpResponse(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });

            var service = ServiceFactory.CreateService(config.Object, httpClient);

            // Act & Assert
            var ex = Assert.ThrowsAsync<OpenAiException>(async () =>
                await service.AskQuestion("test question"));
            Assert.That(ex.Message, Does.Contain("Response JSON missing 'choices[0].message.content'"));
        }

        /// <summary>
        /// Test that: Given a network error, when asking a question, then an OpenAiException is thrown
        /// </summary>
        [Test]
        public void GivenNetworkError_WhenAskingQuestion_ThenThrowsOpenAiException()
        {
            // Arrange
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var service = ServiceFactory.CreateService(config.Object, httpClient);

            // Act & Assert
            var ex = Assert.ThrowsAsync<OpenAiException>(async () =>
                await service.AskQuestion("test question"));
            Assert.That(ex.Message, Does.Contain("HTTP request to OpenAI failed."));
        }
    }
} 