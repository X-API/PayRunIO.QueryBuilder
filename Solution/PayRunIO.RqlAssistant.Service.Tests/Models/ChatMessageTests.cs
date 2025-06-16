namespace PayRunIO.RqlAssistant.Service.Tests.Models
{
    using PayRunIO.RqlAssistant.Service.Models;

    [TestFixture]
    public class ChatMessageTests
    {
        [Test]
        public void ChatMessage_Properties_CanBeSetAndRetrieved()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = ParticipantType.User,
                Text = "Test message"
            };

            // Assert
            Assert.That(message.Role, Is.EqualTo(ParticipantType.User));
            Assert.That(message.Text, Is.EqualTo("Test message"));
        }

        [Test]
        public void ChatMessage_Properties_CanBeModified()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = ParticipantType.User,
                Text = "Initial message"
            };

            // Act
            message.Role = ParticipantType.Assistant;
            message.Text = "Updated message";

            // Assert
            Assert.That(message.Role, Is.EqualTo(ParticipantType.Assistant));
            Assert.That(message.Text, Is.EqualTo("Updated message"));
        }
    }
} 