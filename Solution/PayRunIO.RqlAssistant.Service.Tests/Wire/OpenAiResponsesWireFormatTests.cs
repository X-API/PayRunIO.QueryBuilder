namespace PayRunIO.RqlAssistant.Service.Tests.Wire
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Dtos;
    using PayRunIO.RqlAssistant.Service.Models;
    using PayRunIO.RqlAssistant.Service.Wire;

    [TestFixture]
    public class OpenAiResponsesWireFormatTests
    {
        private static ChatMessage Msg(ParticipantType role, string text) => new() { Role = role, Text = text };

        private static ToolDescriptor Tool() =>
            new("get_schema", "Fetch a schema.", "{\"type\":\"object\",\"properties\":{},\"required\":[]}");

        [Test]
        public void BuildRequestJson_MapsRolesToolsAndStatelessness()
        {
            var format = new OpenAiResponsesWireFormat();

            var json = format.BuildRequestJson(
                new[]
                    {
                        Msg(ParticipantType.User, "hello"),
                        Msg(ParticipantType.System, "be helpful")
                    },
                new[] { Tool() },
                "gpt-5.6-sol",
                0.2);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.GetProperty("model").GetString(), Is.EqualTo("gpt-5.6-sol"));
            Assert.That(root.GetProperty("store").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("temperature").GetDouble(), Is.EqualTo(0.2));
            Assert.That(root.GetProperty("tool_choice").GetString(), Is.EqualTo("auto"));

            // System messages are pinned first even when supplied later.
            var input = root.GetProperty("input").EnumerateArray().ToArray();
            Assert.That(input[0].GetProperty("role").GetString(), Is.EqualTo("system"));
            Assert.That(input[1].GetProperty("role").GetString(), Is.EqualTo("user"));

            // Responses API tools are flat — name at the top level, no nested 'function' object.
            var tool = root.GetProperty("tools").EnumerateArray().Single();
            Assert.That(tool.GetProperty("type").GetString(), Is.EqualTo("function"));
            Assert.That(tool.GetProperty("name").GetString(), Is.EqualTo("get_schema"));
            Assert.That(tool.TryGetProperty("function", out _), Is.False);
        }

        [Test]
        public void BuildRequestJson_ReasoningEffort_ReplacesTemperature()
        {
            var format = new OpenAiResponsesWireFormat("medium");

            var json = format.BuildRequestJson(
                new[] { Msg(ParticipantType.User, "hi") }, null, "gpt-5.6-sol", 0.2);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.GetProperty("reasoning").GetProperty("effort").GetString(), Is.EqualTo("medium"));
            Assert.That(root.TryGetProperty("temperature", out _), Is.False);
        }

        [Test]
        public void BuildRequestJson_ToolCallRoundTrip_EmitsFunctionCallAndOutputItems()
        {
            var format = new OpenAiResponsesWireFormat();

            var assistantWithCall = new ChatMessage
                {
                    Role = ParticipantType.Assistant,
                    Text = string.Empty,
                    ToolCalls = new List<OpenAiToolCall> { new("call_123", "get_schema", "{\"typeName\":\"Employee\"}") }
                };

            var toolReply = new ChatMessage
                {
                    Role = ParticipantType.Tool,
                    ToolCallId = "call_123",
                    Text = "{\"name\":\"Employee\"}"
                };

            var json = format.BuildRequestJson(
                new[] { Msg(ParticipantType.User, "q"), assistantWithCall, toolReply },
                new[] { Tool() },
                "gpt-5.6-sol",
                0.2);

            using var doc = JsonDocument.Parse(json);
            var input = doc.RootElement.GetProperty("input").EnumerateArray().ToArray();

            var call = input.Single(i => i.TryGetProperty("type", out var t) && t.GetString() == "function_call");
            Assert.That(call.GetProperty("call_id").GetString(), Is.EqualTo("call_123"));
            Assert.That(call.GetProperty("name").GetString(), Is.EqualTo("get_schema"));
            Assert.That(call.GetProperty("arguments").GetString(), Does.Contain("Employee"));

            var output = input.Single(i => i.TryGetProperty("type", out var t) && t.GetString() == "function_call_output");
            Assert.That(output.GetProperty("call_id").GetString(), Is.EqualTo("call_123"));
            Assert.That(output.GetProperty("output").GetString(), Does.Contain("Employee"));
        }

        [Test]
        public void ParseResponse_MessageOutput_ReturnsContent()
        {
            const string Body = """
                {
                  "output": [
                    { "type": "reasoning", "summary": [] },
                    { "type": "message", "role": "assistant", "content": [ { "type": "output_text", "text": "Hello there." } ] }
                  ]
                }
                """;

            var response = new OpenAiResponsesWireFormat().ParseResponse(Body, HttpStatusCode.OK);

            Assert.That(response.Content, Is.EqualTo("Hello there."));
            Assert.That(response.ToolCalls, Is.Empty);
        }

        [Test]
        public void ParseResponse_FunctionCallOutput_ReturnsToolCallWithCallId()
        {
            const string Body = """
                {
                  "output": [
                    { "type": "function_call", "id": "fc_abc", "call_id": "call_9", "name": "list_routes", "arguments": "{\"verb\":\"GET\"}" }
                  ]
                }
                """;

            var response = new OpenAiResponsesWireFormat().ParseResponse(Body, HttpStatusCode.OK);

            Assert.That(response.HasToolCalls, Is.True);
            var call = response.ToolCalls.Single();
            Assert.That(call.Id, Is.EqualTo("call_9"), "call_id, not the item id, pairs with function_call_output");
            Assert.That(call.FunctionName, Is.EqualTo("list_routes"));
            Assert.That(call.ArgumentsJson, Does.Contain("GET"));
        }

        [Test]
        public void ParseResponse_NoUsableOutput_Throws()
        {
            Assert.Throws<OpenAiException>(
                new TestDelegate(() => new OpenAiResponsesWireFormat().ParseResponse("{\"output\":[]}", HttpStatusCode.OK)));
        }

        [Test]
        public void BuildRequestUrl_AppendsSuffixOnce()
        {
            var format = new OpenAiResponsesWireFormat();

            Assert.That(format.BuildRequestUrl("https://api.openai.com"), Is.EqualTo("https://api.openai.com/v1/responses"));
            Assert.That(format.BuildRequestUrl("https://api.openai.com/v1/responses"), Is.EqualTo("https://api.openai.com/v1/responses"));
        }

        [Test]
        public void BuildRequestUrl_ReplacesAnotherApisPathSuffix()
        {
            // The selected provider decides the path — a stale endpoint saved with a different
            // API's path must not produce /v1/responses/v1/chat/completions style URLs.
            Assert.That(
                new OpenAiResponsesWireFormat().BuildRequestUrl("https://api.openai.com/v1/chat/completions"),
                Is.EqualTo("https://api.openai.com/v1/responses"));

            Assert.That(
                new OpenAiWireFormat().BuildRequestUrl("https://api.openai.com/v1/responses"),
                Is.EqualTo("https://api.openai.com/v1/chat/completions"));

            Assert.That(
                new AnthropicWireFormat().BuildRequestUrl("https://api.anthropic.com/v1/chat/completions"),
                Is.EqualTo("https://api.anthropic.com/v1/messages"));
        }

        [Test]
        public void BuildRequestUrl_PreservesUnrecognisedCustomPaths()
        {
            Assert.That(
                new OpenAiResponsesWireFormat().BuildRequestUrl("https://myproxy.example/openai"),
                Is.EqualTo("https://myproxy.example/openai/v1/responses"));
        }

        [Test]
        public void ExtractErrorMessage_ReadsErrorObjectMessage()
        {
            const string Body = "{\"error\":{\"message\":\"Function tools are not supported.\"}}";

            Assert.That(
                new OpenAiResponsesWireFormat().ExtractErrorMessage(Body),
                Is.EqualTo("Function tools are not supported."));
        }
    }

    [TestFixture]
    public class OpenAiWireFormatReasoningTests
    {
        [Test]
        public void BuildRequestJson_ReasoningEffort_EmitsReasoningEffortAndOmitsTemperature()
        {
            var format = new OpenAiWireFormat("none");

            var json = format.BuildRequestJson(
                new[] { new ChatMessage { Role = ParticipantType.User, Text = "hi" } },
                null,
                "gpt-5.6-sol",
                0.2);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.GetProperty("reasoning_effort").GetString(), Is.EqualTo("none"));
            Assert.That(root.TryGetProperty("temperature", out _), Is.False);
        }

        [Test]
        public void BuildRequestJson_NoReasoningEffort_KeepsTemperature()
        {
            var format = new OpenAiWireFormat();

            var json = format.BuildRequestJson(
                new[] { new ChatMessage { Role = ParticipantType.User, Text = "hi" } },
                null,
                "gpt-4o-mini",
                0.2);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.GetProperty("temperature").GetDouble(), Is.EqualTo(0.2));
            Assert.That(root.TryGetProperty("reasoning_effort", out _), Is.False);
        }
    }

    [TestFixture]
    public class ProviderTypeParserResponsesTests
    {
        [TestCase("OpenAI (Responses)")]
        [TestCase("OpenAIResponses")]
        [TestCase("openai responses")]
        [TestCase("Responses")]
        public void ParseOrDefault_ResponsesVariants_ReturnOpenAiResponses(string name)
        {
            Assert.That(ProviderTypeParser.ParseOrDefault(name), Is.EqualTo(ProviderType.OpenAiResponses));
        }

        [TestCase("OpenAI", ProviderType.OpenAi)]
        [TestCase("Anthropic", ProviderType.Anthropic)]
        [TestCase(null, ProviderType.OpenAi)]
        [TestCase("", ProviderType.OpenAi)]
        [TestCase("SomethingElse", ProviderType.OpenAi)]
        public void ParseOrDefault_ExistingBehaviour_Unchanged(string? name, ProviderType expected)
        {
            Assert.That(ProviderTypeParser.ParseOrDefault(name), Is.EqualTo(expected));
        }
    }
}
