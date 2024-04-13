namespace Entities;

public class PlayerInfoEntity
{
    public int? user_id { get; set; }
    public string? name { get; set; }
    public ulong steam_id { get; set; }
    public string? account_id { get; set; }
    public string? ip_address { get; set; }

    public long joined_time { get; set; } = 0;

    // Game-specific information
    public bool is_bot { get; set; }
    public int kills { get; set; }
    public int deaths { get; set; }
    public int score { get; set; }

    // Network/technical information
    public uint ping { get; set; }
    public bool is_spectator { get; internal set; }
    public bool is_terrorist { get; internal set; }
    public bool is_counter_terrorist { get; internal set; }
}
