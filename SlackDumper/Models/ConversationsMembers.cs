namespace SlackDumper.Models
{
    public class ConversationsMembers
    {
        public bool ok { get; set; }
        public string[] members { get; set; }
        public ResponseMetadata response_metadata { get; set; }
    }
}
