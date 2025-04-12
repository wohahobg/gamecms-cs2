using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Plugin;
using CounterStrikeSharp.API.Modules.Admin;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Reflection;
using static GameCMS.GameCMSPlugin;

namespace GameCMS.Services
{
    public class VipPlayerEntity
    {
        public int server_id { get; set; }
        public string steam_id { get; set; } = string.Empty;
        public string username { get; set; } = string.Empty;
        public string flags { get; set; } = string.Empty;
        public long created { get; set; }
        public long last_seen { get; set; }
    }

    public class VipPlayerService
    {
        private Helper _helper;
        private HttpClient httpClient = new HttpClient();
        private ILogger<PlayingTimeService> _logger;
        private IPluginContext _pluginContext;
        private GameCMSPlugin? _plugin;
        private MySqlConnection? _connection;
        private int _serverId;

        private int roundsWinsCt = 0;
        private int roundsWinsT = 0;
        private long matchStartTime = 0;
        private bool isWarmupRound = true;
        private static bool isRequestInProgress = false;
        private static readonly TimeSpan requestCooldown = TimeSpan.FromSeconds(3);
        private static DateTime lastRequestTime = DateTime.MinValue;

        public VipPlayerService(ILogger<PlayingTimeService> logger, Helper helper, IPluginContext pluginContext)
        {
            _helper = helper;
            _logger = logger;
            _pluginContext = pluginContext;
        }

        public void Start(bool hotReload, int serverId)
        {
            _serverId = serverId;
            _plugin = (_pluginContext.Plugin as GameCMSPlugin)!;
            _logger.LogInformation("Starting vip player collection service");

            if (!_plugin.Config.services.VipServices.Enabled) return;

            _plugin.RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.VipServices.Enabled) return HookResult.Continue;

                CSSThread.RunOnMainThread(() =>
                {
                    _ = PurgeVipPlayersAsync();
                });

                return HookResult.Continue;
            });

            _plugin.RegisterEventHandler((EventServerShutdown @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.VipServices.Enabled) return HookResult.Continue;

                CSSThread.RunOnMainThread(() =>
                {
                    _ = PurgeVipPlayersAsync();
                });

                return HookResult.Continue;
            });

            _plugin.RegisterEventHandler((EventPlayerActivate @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.VipServices.Enabled) return HookResult.Continue;
                CCSPlayerController player = @event.Userid!;
                if (player == null || _helper.isValidPlayer(player) == false || player.IsHLTV || player.IsBot)
                    return HookResult.Continue;

                _plugin.AddTimer(3.0f, () =>
                {
                    CSSThread.RunOnMainThread(async () =>
                    {
                        try
                        {
                            AdminData adminData = AdminManager.GetPlayerAdminData(player)!;

                            if (adminData == null || adminData.Flags.Any() == false) return;
                            
                            string[] serviceFlags = _plugin.Config.services.VipServices.FlagsList;

                            // Normalize flags by removing @ prefix if present
                            var normalizedAdminFlags = adminData.Flags.Values
                                .SelectMany(x => x)
                                .Select(flag => flag.StartsWith("@") ? flag.Substring(1) : flag)
                                .ToList();

                            bool hasVipFlag = normalizedAdminFlags
                                .Intersect(serviceFlags)
                                .Any();

                            if (hasVipFlag)
                            {
                                try
                                {
                                    if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                                    {
                                        _connection = await Database.Instance.GetConnection();
                                    }

                                    var filteredFlags = normalizedAdminFlags
                                        .Where(flag => serviceFlags.Contains(flag))
                                        .ToList();

                                    var vipPlayer = new VipPlayerEntity
                                    {
                                        server_id = _serverId,
                                        steam_id = player.SteamID.ToString(),
                                        username = player.PlayerName,
                                        flags = string.Join(",", filteredFlags),
                                        created = _helper.GetTime(),
                                        last_seen = _helper.GetTime()
                                    };

                                    await AddVipPlayerAsync(vipPlayer);
                                }
                                catch (Exception dbEx)
                                {
                                    _logger.LogError($"Database error for player {player.PlayerName}: {dbEx.Message}");
                                    if (_connection != null)
                                    {
                                        await _connection.CloseAsync();
                                        _connection = null;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing VIP player {player.PlayerName}: {ex.Message}");
                        }
                    });
                });

                return HookResult.Continue;
            });
        }

        public async Task AddVipPlayerAsync(VipPlayerEntity vipPlayer)
        {
            var properties = typeof(VipPlayerEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var columnNames = properties.Select(p => $"`{p.Name}`");
            var columnParameters = properties.Select(p => $"@{p.Name}");

            var columns = string.Join(", ", columnNames);
            var values = string.Join(", ", columnParameters);

            // Create the ON DUPLICATE KEY UPDATE part
            var updateColumns = properties
                .Where(p => p.Name != "id")
                .Select(p => $"`{p.Name}` = VALUES(`{p.Name}`)");
            var updateClause = string.Join(", ", updateColumns);

            var sql = $@"
                INSERT INTO gcms_vip_players ({columns}) 
                VALUES ({values})
                ON DUPLICATE KEY UPDATE 
                    last_seen = VALUES(last_seen),
                    username = VALUES(username),
                    flags = VALUES(flags)";

            await _connection!.ExecuteAsync(sql, vipPlayer);
        }

        public async Task PurgeVipPlayersAsync()
        {
            string query = "DELETE FROM gcms_vip_players WHERE last_seen < @time";
            await _connection!.ExecuteAsync(query, new
            {
                time = _helper.GetTime() - (_plugin!.Config.services.VipServices.PurgeDays * 86400)
            });
        }
    }
}
