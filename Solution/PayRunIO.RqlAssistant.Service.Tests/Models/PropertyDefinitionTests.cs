namespace PayRunIO.RqlAssistant.Service.Tests.Models
{
    using PayRunIO.RqlAssistant.Service.Models;

    [TestFixture]
    public class PropertyDefinitionTests
    {
        [Test]
        public void PropertyDefinition_Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var property = new PropertyDefinition
            {
                Name = "TestProperty",
                Type = "string",
                Description = "Test property description"
            };

            // Assert
            Assert.That(property.Name, Is.EqualTo("TestProperty"));
            Assert.That(property.Type, Is.EqualTo("string"));
            Assert.That(property.Description, Is.EqualTo("Test property description"));
        }

        [Test]
        public void PropertyDefinition_ToString_ReturnsFormattedString()
        {
            // Arrange
            var property = new PropertyDefinition
            {
                Name = "TestProperty",
                Type = "string",
                Description = "Test property description"
            };

            // Act
            var result = property.ToString();

            // Assert
            Assert.That(result, Is.EqualTo(" * Name: TestProperty * Type: string * Description: Test property description"));
        }

        [Test]
        public void PropertyDefinition_WithEmptyProperties_ToStringStillWorks()
        {
            // Arrange
            var property = new PropertyDefinition();

            // Act
            var result = property.ToString();

            // Assert
            Assert.That(result, Is.EqualTo(" * Name:  * Type:  * Description: "));
        }
    }
} 