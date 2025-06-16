namespace PayRunIO.RqlAssistant.Service.Tests.Models
{
    using PayRunIO.RqlAssistant.Service.Models;

    [TestFixture]
    public class RouteDefinitionTests
    {
        [Test]
        public void RouteDefinition_Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var route = new RouteDefinition
            {
                ClassName = "TestClass",
                Route = "/api/test",
                RouteSignature = "GET /api/test",
                OperationId = "getTest",
                Verb = "GET",
                Summary = "Test summary",
                Description = "Test description",
                Tags = new List<string> { "test", "api" },
                ResponseCode = 200,
                ResponseType = "TestResponse"
            };

            // Assert
            Assert.That(route.ClassName, Is.EqualTo("TestClass"));
            Assert.That(route.Route, Is.EqualTo("/api/test"));
            Assert.That(route.RouteSignature, Is.EqualTo("GET /api/test"));
            Assert.That(route.OperationId, Is.EqualTo("getTest"));
            Assert.That(route.Verb, Is.EqualTo("GET"));
            Assert.That(route.Summary, Is.EqualTo("Test summary"));
            Assert.That(route.Description, Is.EqualTo("Test description"));
            Assert.That(route.Tags, Is.EquivalentTo(new[] { "test", "api" }));
            Assert.That(route.ResponseCode, Is.EqualTo(200));
            Assert.That(route.ResponseType, Is.EqualTo("TestResponse"));
        }

        [Test]
        public void RouteDefinition_ToString_ReturnsFormattedString()
        {
            // Arrange
            var route = new RouteDefinition
            {
                ClassName = "TestClass",
                RouteSignature = "GET /api/test",
                Description = "Test description",
                ResponseType = "TestResponse"
            };

            // Act
            var result = route.ToString();

            // Assert
            var expected = "# API Route Name: TestClass\r\n" +
                          "* API Route Signature: GET /api/test\r\n" +
                          "* Description: Test description\r\n" +
                          "* ResponseType: TestResponse\r\n" +
                          "---\r\n";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void RouteDefinition_WithEmptyProperties_ToStringStillWorks()
        {
            // Arrange
            var route = new RouteDefinition();

            // Act
            var result = route.ToString();

            // Assert
            var expected = "# API Route Name: \r\n" +
                          "* API Route Signature: \r\n" +
                          "* Description: \r\n" +
                          "* ResponseType: \r\n" +
                          "---\r\n";
            Assert.That(result, Is.EqualTo(expected));
        }
    }
} 