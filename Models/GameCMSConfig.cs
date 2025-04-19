using System.Collections.Generic;

namespace GameCMS.Models
{
    public class GameCMSConfig
    {
        public string ServerApiKey { get; set; } = string.Empty;
        public string WebsiteApiKey { get; set; } = string.Empty;
        public int ServerHttpPort { get; set; }
        public string FaceitToken { get; set; } = string.Empty;
        public bool DeleteExpiredAdmins { get; set; }
        public ServicesConfig Services { get; set; } = new ServicesConfig();
        public DatabaseConfig Database { get; set; } = new DatabaseConfig();
        public int ConfigVersion { get; set; }
    }

    public class ServicesConfig
    {
        public bool PlayingTime { get; set; }
        public bool ServerDataCollection { get; set; }
        public VipStatusTrackerConfig VipStatusTracker { get; set; } = new VipStatusTrackerConfig();
    }

    public class VipStatusTrackerConfig
    {
        public string Comment { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public List<string> FlagsList { get; set; } = new List<string>();
        public int PurgeDays { get; set; }
    }

    public class DatabaseConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
} 