namespace chat_app_backend
{
    public class Message
    {
        public MessageType Type { get; set; }
        public string Sender { get; set; }
        public string Body { get; set; }

    }

    public enum MessageType
    {
        MESSAGE,
        UTILITY,
        ALL_MESSAGES,
        ASSIGN_USER,
        CLOSE
    }
}
