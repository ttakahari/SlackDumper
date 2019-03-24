using System.Collections.Generic;

namespace SlackDumper.Models
{
    public class ConversationsList
    {
        public bool ok { get; set; }
        public IReadOnlyDictionary<string, object>[] channels { get; set; }
        public ResponseMetadata response_metadata { get; set; }
    }
}
