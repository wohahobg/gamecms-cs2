namespace Entities
{

    public class AdminEntity
    {
        public ulong id { get; set; }
        public string identity { get; set; } = string.Empty;
        public string player_name { get; set; } = string.Empty;
        public long server_id { get; set; } = 0;
        public string groups { get; set; } = string.Empty;
        public string flags { get; set; } = string.Empty;
        public string overrides { get; set; } = string.Empty;
        public int immunity { get; set; } = 0;
        public long expiry { get; set; }
        public long created { get; set; }
    }


    public class AdminGroupEntity
    {
        public ulong? id { get; set; }
        public long server_id { get; set; }
        public string flags { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public int immunity { get; set; } = 0;
    }

    public class AdminOverrideEntity
    {
        public ulong? id { get; set; }
        public long server_id { get; set; }
        public string check_type { get; set; } = string.Empty;
        public int enabled { get; set; } = 0;
        public string name { get; set; } = string.Empty;
        public string flags { get; set; } = string.Empty;
    }
}

