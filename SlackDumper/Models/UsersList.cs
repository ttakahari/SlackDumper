using System.Collections.Generic;

namespace SlackDumper.Models
{
    public class UsersList
    {
        public bool ok { get; set; }
        public IReadOnlyDictionary<string, object>[] members { get; set; }
        public int cache_ts { get; set; }
        public ResponseMetadata response_metadata { get; set; }
    }
}
