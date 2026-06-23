namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Linq;
    using System.Text.Json;

    using Moq;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;

    [TestFixture]
    public class RqlRagServiceTests
    {
        private Mock<IRequestBuilderService> requestBuilder = null!;

        private Mock<IRemoteAiService> remote = null!;

        private Mock<IRqlToolDispatcher> dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            this.requestBuilder = new Mock<IRequestBuilderService>();
            this.requestBuilder
                .Setup(r => r.CreateAiRequestJson(It.IsAny<ChatMessage[]>(), It.IsAny<IReadOnlyList<ToolDescriptor>?>(), It.IsAny<string?>(), It.IsAny<double?>()))
                .Returns("{}");

            this.remote = new Mock<IRemoteAiService>();

            this.dispatcher = new Mock<IRqlToolDispatcher>();
            this.dispatcher.SetupGet(d => d.Descriptors).Returns(new List<ToolDescriptor>());
        }

        private RqlRagService CreateService() =>
            new RqlRagService(this.requestBuilder.Object, this.remote.Object, this.dispatcher.Object);

        private static OpenAiChatResponse FinalContent(string text) =>
            new OpenAiChatResponse { Content = text };

        private static OpenAiChatResponse ToolCalls(params (string id, string name, string argsJson)[] calls) =>
            new OpenAiChatResponse
                {
                    ToolCalls = calls.Select(c => new OpenAiToolCall(c.id, c.name, c.argsJson)).ToArray()
                };

        [Test]
        public void AskQuestion_NullOrWhitespaceQuestion_ThrowsArgumentException()
        {
            var service = this.CreateService();

            Func<Task<string>> funcA = () => service.AskQuestion(null!);
            Func<Task<string>> funcB = () => service.AskQuestion(string.Empty);
            Func<Task<string>> funcC = () => service.AskQuestion("   ");

            Assert.ThrowsAsync<ArgumentException>(funcA);
            Assert.ThrowsAsync<ArgumentException>(funcB);
            Assert.ThrowsAsync<ArgumentException>(funcC);
        }

        [Test]
        public async Task AskQuestion_FinalContentOnFirstTurn_ReturnsContentImmediately()
        {
            this.remote
                .Setup(r => r.GetChatResponseAsync("{}", default))
                .ReturnsAsync(FinalContent("Here is your reply."));

            var service = this.CreateService();

            var result = await service.AskQuestion("Show me employees");

            Assert.That(result, Is.EqualTo("Here is your reply."));
            this.dispatcher.Verify(d => d.Dispatch(It.IsAny<string>(), It.IsAny<JsonElement>()), Times.Never);
        }

        [Test]
        public async Task AskQuestion_OneToolCallThenFinalContent_DispatchesAndReturnsContent()
        {
            this.remote
                .SetupSequence(r => r.GetChatResponseAsync(It.IsAny<string>(), default))
                .ReturnsAsync(ToolCalls(("call-1", "get_schema", @"{""typeName"":""Employee""}")))
                .ReturnsAsync(FinalContent("Done."));

            this.dispatcher
                .Setup(d => d.Dispatch("get_schema", It.IsAny<JsonElement>()))
                .Returns(@"{""Name"":""Employee""}");

            var service = this.CreateService();

            var result = await service.AskQuestion("Show me employees");

            Assert.That(result, Is.EqualTo("Done."));
            this.dispatcher.Verify(d => d.Dispatch("get_schema", It.IsAny<JsonElement>()), Times.Once);
        }

        [Test]
        public async Task AskQuestion_DispatcherReceivesParsedJsonElementArguments()
        {
            this.remote
                .SetupSequence(r => r.GetChatResponseAsync(It.IsAny<string>(), default))
                .ReturnsAsync(ToolCalls(("c1", "get_schema", @"{""typeName"":""Employee""}")))
                .ReturnsAsync(FinalContent("ok"));

            JsonElement captured = default;
            this.dispatcher
                .Setup(d => d.Dispatch("get_schema", It.IsAny<JsonElement>()))
                .Callback<string, JsonElement>((_, args) => captured = args.Clone())
                .Returns("{}");

            var service = this.CreateService();

            await service.AskQuestion("q");

            Assert.That(captured.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(captured.GetProperty("typeName").GetString(), Is.EqualTo("Employee"));
        }

        [Test]
        public async Task AskQuestion_MultipleToolCallsInOneTurn_AllDispatched()
        {
            this.remote
                .SetupSequence(r => r.GetChatResponseAsync(It.IsAny<string>(), default))
                .ReturnsAsync(ToolCalls(
                    ("c1", "get_schema", @"{""typeName"":""Employee""}"),
                    ("c2", "list_routes", @"{""verb"":""GET""}")))
                .ReturnsAsync(FinalContent("Done."));

            this.dispatcher.Setup(d => d.Dispatch("get_schema", It.IsAny<JsonElement>())).Returns("{}");
            this.dispatcher.Setup(d => d.Dispatch("list_routes", It.IsAny<JsonElement>())).Returns("[]");

            var service = this.CreateService();

            var result = await service.AskQuestion("q");

            Assert.That(result, Is.EqualTo("Done."));
            this.dispatcher.Verify(d => d.Dispatch("get_schema", It.IsAny<JsonElement>()), Times.Once);
            this.dispatcher.Verify(d => d.Dispatch("list_routes", It.IsAny<JsonElement>()), Times.Once);
        }

        [Test]
        public async Task AskQuestion_ToolResultsAreFedBackAsToolMessages()
        {
            this.remote
                .SetupSequence(r => r.GetChatResponseAsync(It.IsAny<string>(), default))
                .ReturnsAsync(ToolCalls(("c1", "get_schema", "{}")))
                .ReturnsAsync(FinalContent("ok"));

            this.dispatcher
                .Setup(d => d.Dispatch("get_schema", It.IsAny<JsonElement>()))
                .Returns(@"{""Name"":""Employee""}");

            // Capture every invocation's messages and assert against the final one — that's the call after
            // tool dispatch, so it carries the tool reply we want to verify.
            ChatMessage[]? lastMessages = null;
            this.requestBuilder
                .Setup(r => r.CreateAiRequestJson(It.IsAny<ChatMessage[]>(), It.IsAny<IReadOnlyList<ToolDescriptor>?>(), It.IsAny<string?>(), It.IsAny<double?>()))
                .Callback<ChatMessage[], IReadOnlyList<ToolDescriptor>?, string?, double?>((msgs, _, _, _) => lastMessages = msgs)
                .Returns("{}");

            var service = this.CreateService();

            await service.AskQuestion("q");

            Assert.That(lastMessages, Is.Not.Null);
            var assistantToolCallMessage = lastMessages!.FirstOrDefault(m => m.Role == ParticipantType.Assistant && m.ToolCalls?.Count > 0);
            var toolReply = lastMessages.FirstOrDefault(m => m.Role == ParticipantType.Tool);

            Assert.That(assistantToolCallMessage, Is.Not.Null, "Expected an assistant message carrying the tool_calls.");
            Assert.That(toolReply, Is.Not.Null, "Expected a tool reply message for the dispatched call.");
            Assert.That(toolReply!.ToolCallId, Is.EqualTo("c1"));
            Assert.That(toolReply.Text, Is.EqualTo(@"{""Name"":""Employee""}"));
        }

        [Test]
        public async Task AskQuestion_InvalidToolArgumentsJson_DispatcherReceivesErrorAndLoopContinues()
        {
            this.remote
                .SetupSequence(r => r.GetChatResponseAsync(It.IsAny<string>(), default))
                .ReturnsAsync(ToolCalls(("c1", "get_schema", "this is not json")))
                .ReturnsAsync(FinalContent("recovered"));

            // Dispatcher should not be called when the JSON fails to parse — the rag service should
            // surface a structured error in the tool reply itself.
            this.dispatcher
                .Setup(d => d.Dispatch(It.IsAny<string>(), It.IsAny<JsonElement>()))
                .Returns("{}");

            ChatMessage[]? lastMessages = null;
            this.requestBuilder
                .Setup(r => r.CreateAiRequestJson(It.IsAny<ChatMessage[]>(), It.IsAny<IReadOnlyList<ToolDescriptor>?>(), It.IsAny<string?>(), It.IsAny<double?>()))
                .Callback<ChatMessage[], IReadOnlyList<ToolDescriptor>?, string?, double?>((msgs, _, _, _) => lastMessages = msgs)
                .Returns("{}");

            var service = this.CreateService();

            var result = await service.AskQuestion("q");

            Assert.That(result, Is.EqualTo("recovered"));
            this.dispatcher.Verify(d => d.Dispatch(It.IsAny<string>(), It.IsAny<JsonElement>()), Times.Never);

            var toolReply = lastMessages!.First(m => m.Role == ParticipantType.Tool);
            using var doc = JsonDocument.Parse(toolReply.Text);
            Assert.That(doc.RootElement.GetProperty("error").GetString(), Does.Contain("not valid JSON"));
        }

        [Test]
        public void AskQuestion_ExceedsIterationCap_ThrowsOpenAiException()
        {
            // Always return a tool call — never a final reply. After MaxIterations (=10) the service must give up.
            this.remote
                .Setup(r => r.GetChatResponseAsync(It.IsAny<string>(), default))
                .ReturnsAsync(ToolCalls(("c", "get_schema", "{}")));

            this.dispatcher
                .Setup(d => d.Dispatch(It.IsAny<string>(), It.IsAny<JsonElement>()))
                .Returns("{}");

            var service = this.CreateService();

            Func<Task<string>> func = () => service.AskQuestion("q");

            var ex = Assert.ThrowsAsync<OpenAiException>(func);
            Assert.That(ex!.Message, Does.Contain("maximum"));

            this.remote.Verify(r => r.GetChatResponseAsync(It.IsAny<string>(), default), Times.Exactly(10));
        }

        [Test]
        public void AskQuestion_RemoteAiServiceThrows_PropagatesException()
        {
            this.remote
                .Setup(r => r.GetChatResponseAsync(It.IsAny<string>(), default))
                .ThrowsAsync(new OpenAiException("network failure"));

            var service = this.CreateService();

            Func<Task<string>> func = () => service.AskQuestion("q");

            var ex = Assert.ThrowsAsync<OpenAiException>(func);
            Assert.That(ex!.Message, Is.EqualTo("network failure"));
        }
    }
}
