namespace PayRunIO.RqlAssistant.Service.Tests
{
    [TestFixture]
    public class ResourceHelperTests
    {
        [Test]
        public async Task LoadResourceAsStringAsync_WithExistingResource_ReturnsContent()
        {
            // Arrange
            var resourceName = ResourceHelper.FindSchemaAndRouteNames;

            // Act
            var result = await ResourceHelper.LoadResourceAsStringAsync(resourceName);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
        }

        [Test]
        public void LoadResourceAsStringAsync_WithNonExistentResource_ThrowsInvalidOperationException()
        {
            // Arrange
            var nonExistentResource = "non-existent-resource.txt";

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ResourceHelper.LoadResourceAsStringAsync(nonExistentResource));

            Assert.That(exception.Message, Does.Contain(nonExistentResource));
        }

        [Test]
        public async Task LoadResourceAsStringAsync_WithAllKnownResources_LoadsSuccessfully()
        {
            // Arrange
            var knownResources = new[]
            {
                ResourceHelper.FindSchemaAndRouteNames,
                ResourceHelper.AnswerQuestionSystemPrompt,
                ResourceHelper.TabularRql,
                ResourceHelper.RqlDocJson,
                ResourceHelper.RqlDocXml,
                ResourceHelper.QuerySchema,
                ResourceHelper.Routes,
                ResourceHelper.Dtos
            };

            // Act & Assert
            foreach (var resource in knownResources)
            {
                var result = await ResourceHelper.LoadResourceAsStringAsync(resource);
                Assert.That(result, Is.Not.Null, $"Resource {resource} should not be null");
                Assert.That(result, Is.Not.Empty, $"Resource {resource} should not be empty");
            }
        }
    }
} 