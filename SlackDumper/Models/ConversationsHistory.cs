namespace SlackDumper.Models
{
    public class ConversationsHistory
    {
        public bool ok { get; set; }
        public Message[] messages { get; set; }
        public bool has_more { get; set; }
        public int pin_count { get; set; }
        public ResponseMetadata response_metadata { get; set; }
    }

    public class Message
    {
        public string type { get; set; }
        public string user { get; set; }
        public string text { get; set; }
        public string ts { get; set; }
        public Attachment[] attachments { get; set; }
    }

    public class Attachment
    {
        public string service_name { get; set; }
        public string text { get; set; }
        public string fallback { get; set; }
        public string thumb_url { get; set; }
        public int thumb_width { get; set; }
        public int thumb_height { get; set; }
        public int id { get; set; }
    }
}
