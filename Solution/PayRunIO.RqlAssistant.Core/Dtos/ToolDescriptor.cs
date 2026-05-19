namespace PayRunIO.RqlAssistant.Service.Dtos
{
    /// <summary>
    /// Describes a tool exposed by <see cref="RqlToolDispatcher"/>. Stable across MCP and OpenAI tool-calling
    /// transports — the latter inlines <see cref="ParametersJsonSchema"/> verbatim under each function's
    /// <c>parameters</c> field.
    /// </summary>
    public sealed record ToolDescriptor(string Name, string Description, string ParametersJsonSchema);
}
