using Producer.Services;

namespace Producer.Tests
{
    public class MessageFactoryTests
    {
        [Test]
        public void Create_ShouldGenerateValidMessage()
        {
            // Arrange
            var factory = new MessageFactory();
            string content = "Message de test SUEZ";

            // Act
            var message = factory.Create(content);

            // Assert
            Assert.That(message.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(message.Message, Is.EqualTo(content));
            Assert.That(message.CreatedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public void Serialize_ShouldContainMessageData()
        {
            // Arrange
            var factory = new MessageFactory();

            var message = factory.Create("Message de test SUEZ");

            // Act
            string json = factory.Serialize(message);

            // Assert
            Assert.That(json, Does.Contain(message.Id.ToString()));
            Assert.That(json, Does.Contain("Message de test SUEZ"));
            Assert.That(json, Does.Contain("CreatedAt"));
        }
    }
}