using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace Main;
public class GameCMSConfig : BasePluginConfig
{
    [JsonPropertyName("ServerApiKey")]
    public string ServerApiKey { get; set; } = "Your Server API KEY";

    [JsonPropertyName("WebsiteApiKey")]
    public string WebsiteApiKey { get; set; } = "Your website API KEY";

    [JsonPropertyName("ServerHttpPort")]
    public int ServerHttpPort { get; set; } = 27017;

    [JsonPropertyName("DeleteExpiredAdmins")]
    public bool DeleteExpiredAdmins { get; set; } = true;

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
