namespace GameCMS
{

    using System.Text.Json.Serialization;
    using CounterStrikeSharp.API.Core;

    public class GameCMSConfig : BasePluginConfig
    {
        [JsonPropertyName("ServerApiKey")]
        public string ServerApiKey { get; set; } = "Your Server API KEY";

        [JsonPropertyName("WebsiteApiKey")]
        public string WebsiteApiKey { get; set; } = "Your website API KEY";

        [JsonPropertyName("ServerHttpPort")]
        public int ServerHttpPort { get; set; } = 27017;

        [JsonPropertyName("FaceitToken")]
        public string FaceitToken { get; set; } = "";

        [JsonPropertyName("DeleteExpiredAdmins")]
        public bool DeleteExpiredAdmins { get; set; } = true;

        [JsonPropertyName("services")]
        public ServicesConfig services { get; set; } = new ServicesConfig();

        [JsonPropertyName("database")]
        public DatabaseConfig database { get; set; } = new DatabaseConfig();
    }

    public sealed class DatabaseConfig
    {

        [JsonPropertyName("host")]
        public string host { get; set; } = "localhost";

        [JsonPropertyName("port")]
        public uint port { get; set; } = 3306;

        [JsonPropertyName("name")]
        public string name { get; set; } = "";

        [JsonPropertyName("username")]
        public string username { get; set; } = "";

        [JsonPropertyName("password")]
        public string password { get; set; } = "";
    }

    public sealed class ServicesConfig
    {
        [JsonPropertyName("playing-time")]
        public bool PlayingTime { get; set; } = true;
        [JsonPropertyName("server-data-collection")]
        public bool ServerDataCollection { get; set; } = true;
        [JsonPropertyName("vip-status-tracker")]
        public VipStatusTrackerConfig VipStatusTracker { get; set; } = new VipStatusTrackerConfig();
    }

    public sealed class VipStatusTrackerConfig
    {
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = "Please visit https://docs.gamecms.org/integrations/counter-strike-2/gamecms-plugins-features for more information what this service does.";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
        [JsonPropertyName("flags-list")]
        public string[] FlagsList { get; set; } = new string[] { "css/vip", "css/vip-plus", "css/vip-premium" };

        [JsonPropertyName("purge-days")]
        public int PurgeDays { get; set; } = 30;
    }

}