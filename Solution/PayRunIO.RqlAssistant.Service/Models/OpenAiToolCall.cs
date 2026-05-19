namespace PayRunIO.RqlAssistant.Service.Models
{
    /// <summary>
    /// A single tool invocation the assistant emitted in lieu of a final reply. Matches the OpenAI
    /// Chat Completions <c>choices[].message.tool_calls[]</c> wire shape: the <see cref="ArgumentsJson"/>
    /// is the raw JSON string the model produced, deliberately un-parsed so the dispatcher decides how
    /// to validate it.
    /// </summary>
    public sealed record OpenAiToolCall(string Id, string FunctionName, string ArgumentsJson);
}
