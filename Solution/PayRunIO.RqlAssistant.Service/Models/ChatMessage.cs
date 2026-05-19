namespace PayRunIO.RqlAssistant.Service.Models
{
    public class ChatMessage
    {
        public ParticipantType Role { get; set; }

        public string Text { get; set; }

        /// <summary>
        /// Populated on <see cref="ParticipantType.Tool"/> messages — correlates the result back to the
        /// assistant <see cref="ToolCalls"/> entry with the matching id.
        /// </summary>
        public string? ToolCallId { get; set; }

        /// <summary>
        /// Populated on <see cref="ParticipantType.Assistant"/> messages when the assistant requested tool
        /// invocations in lieu of a final reply. The dispatcher must process every entry and reply with a
        /// matching <see cref="ParticipantType.Tool"/> message before the next assistant turn.
        /// </summary>
        public IReadOnlyList<OpenAiToolCall>? ToolCalls { get; set; }
    }
}
