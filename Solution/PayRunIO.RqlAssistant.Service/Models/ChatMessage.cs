namespace PayRunIO.RqlAssistant.Service.Models
{
    public class ChatMessage
    {
        public ParticipantType Role { get; set; }

        public string Text { get; set; }
    }
}