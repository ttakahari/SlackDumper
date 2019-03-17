namespace SlackDumper.Models
{
    public class Channels
    {
        public bool ok { get; set; }

        public Channel[] channels { get; set; }

        public ResponseMetadata response_metadata { get; set; }
    }

    public class Channel
    {
        public string id { get; set; }

        public string name { get; set; }

        public bool is_channel { get; set; }

        public bool is_group { get; set; }

        public bool is_im { get; set; }

        public long created { get; set; }

        public string creator { get; set; }

        public bool is_archived { get; set; }

        public bool is_general { get; set; }

        public long unlinked { get; set; }

        public string name_normalized { get; set; }

        public bool is_shared { get; set; }

        public bool is_ext_shared { get; set; }

        public bool is_org_shared { get; set; }

        public string[] pendeing_shared { get; set; }

        public bool is_pending_ext_shared { get; set; }

        public bool is_member { get; set; }

        public bool is_private { get; set; }

        public bool is_mpim { get; set; }

        public Index topic { get; set; }

        public Index purpose { get; set; }

        public string[] previous_names { get; set; }

        public long num_members { get; set; }
    }

    public class Index
    {
        public string value { get; set; }

        public string creator { get; set; }

        public long last_set { get; set; }
    }
}
