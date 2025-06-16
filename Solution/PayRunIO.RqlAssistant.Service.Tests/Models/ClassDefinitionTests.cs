namespace PayRunIO.RqlAssistant.Service.Tests.Models
{
    using PayRunIO.RqlAssistant.Service.Models;

    [TestFixture]
    public class ClassDefinitionTests
    {
        [Test]
        public void ClassDefinition_Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var classDef = new ClassDefinition
            {
                ClassName = "TestClass",
                Description = "Test description",
                Properties = new List<PropertyDefinition>
                {
                    new PropertyDefinition
                    {
                        Name = "Property1",
                        Type = "string",
                        Description = "Property 1 description"
                    },
                    new PropertyDefinition
                    {
                        Name = "Property2",
                        Type = "int",
                        Description = "Property 2 description"
                    }
                }
            };

            // Assert
            Assert.That(classDef.ClassName, Is.EqualTo("TestClass"));
            Assert.That(classDef.Description, Is.EqualTo("Test description"));
            Assert.That(classDef.Properties.Count, Is.EqualTo(2));
            Assert.That(classDef.Properties[0].Name, Is.EqualTo("Property1"));
            Assert.That(classDef.Properties[1].Name, Is.EqualTo("Property2"));
        }

        [Test]
        public void ClassDefinition_ToString_ReturnsFormattedString()
        {
            // Arrange
            var classDef = new ClassDefinition
            {
                ClassName = "TestClass",
                Description = "Test description",
                Properties = new List<PropertyDefinition>
                {
                    new PropertyDefinition
                    {
                        Name = "Property1",
                        Type = "string",
                        Description = "Property 1 description"
                    }
                }
            };

            // Act
            var result = classDef.ToString();

            // Assert
            var expected = " * Class Name: TestClass\r\n" +
                          " * Description: Test description\r\n" +
                          " * Properties: \r\n" +
                          "    * Name: Property1 * Type: string * Description: Property 1 description\r\n";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ClassDefinition_WithEmptyProperties_ToStringStillWorks()
        {
            // Arrange
            var classDef = new ClassDefinition();

            // Act
            var result = classDef.ToString();

            // Assert
            var expected = " * Class Name: \r\n" +
                          " * Description: \r\n" +
                          " * Properties: \r\n";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ClassDefinition_Properties_IsInitializedByDefault()
        {
            // Arrange
            var classDef = new ClassDefinition();

            // Assert
            Assert.That(classDef.Properties, Is.Not.Null);
            Assert.That(classDef.Properties, Is.Empty);
        }
    }
} 