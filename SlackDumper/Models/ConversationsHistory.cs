using System.Collections.Generic;

namespace SlackDumper.Models
{
    public class ConversationsHistory
    {
        public bool ok { get; set; }
        public IReadOnlyDictionary<string, object>[] messages { get; set; }
        public bool has_more { get; set; }
        public int pin_count { get; set; }
        public ResponseMetadata response_metadata { get; set; }
    }
}
