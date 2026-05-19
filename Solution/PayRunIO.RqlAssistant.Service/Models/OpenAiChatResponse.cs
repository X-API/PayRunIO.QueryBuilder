namespace PayRunIO.RqlAssistant.Service.Models
{
    /// <summary>
    /// Structured response from a single OpenAI Chat Completions call. Exactly one of
    /// <see cref="Content"/> or <see cref="ToolCalls"/> is populated:
    /// <list type="bullet">
    ///   <item><description><see cref="Content"/> set: the assistant produced a final reply.</description></item>
    ///   <item><description><see cref="ToolCalls"/> non-empty: the assistant requested tool invocations; the caller
    ///   must dispatch each and feed the results back as <see cref="ParticipantType.Tool"/> messages on the next turn.</description></item>
    /// </list>
    /// </summary>
    public sealed class OpenAiChatResponse
    {
        public string? Content { get; init; }

        public IReadOnlyList<OpenAiToolCall> ToolCalls { get; init; } = Array.Empty<OpenAiToolCall>();

        public bool HasToolCalls => this.ToolCalls.Count > 0;
    }
}
