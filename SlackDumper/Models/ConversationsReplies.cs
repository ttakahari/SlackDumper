using System.Collections.Generic;

namespace SlackDumper.Models
{
    public class ConversationsReplies
    {
        public IReadOnlyDictionary<string, object>[] messages { get; set; }
        public bool ok { get; set; }
        public bool has_more { get; set; }
        public ResponseMetadata response_metadata { get; set; }
    }
}
