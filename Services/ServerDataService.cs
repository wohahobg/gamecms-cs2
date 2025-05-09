using System.Net;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Plugin;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
namespace GameCMS
{


    public class ServerDataService
    {

        private Helper _helper;
        private HttpClient httpClient = new HttpClient();
        private ILogger<PlayingTimeService> _logger;
        private IPluginContext _pluginContext;
        private GameCMSPlugin? _plugin;

        private int roundsWinsCt = 0;
        private int roundsWinsT = 0;
        private long matchStartTime = 0;
        private bool isWarmupRound = true;
        private static bool isRequestInProgress = false;
        private static readonly TimeSpan requestCooldown = TimeSpan.FromSeconds(3);
        private static DateTime lastRequestTime = DateTime.MinValue;

        public ServerDataService(ILogger<PlayingTimeService> logger, Helper helper, IPluginContext pluginContext)
        {
            _helper = helper;
            _logger = logger;
            _pluginContext = pluginContext;
        }


        public void Start(bool hotReload, int serverId)
        {
            _plugin = (_pluginContext.Plugin as GameCMSPlugin)!;
            _logger.LogInformation("Starting server data collection service");
            _plugin.RegisterEventHandler((EventServerShutdown @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.ServerDataCollection) return HookResult.Continue;
                Task.Run(() => SendServerData(false));
                return HookResult.Continue;
            });

            _plugin.RegisterEventHandler((EventPlayerConnectFull @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.ServerDataCollection) return HookResult.Continue;
                CCSPlayerController player = @event.Userid!;
                if (player == null || _helper.isValidPlayer(player) == false || player!.IsHLTV && player.IsBot)
                    return HookResult.Continue;

                Task.Run(() => SendServerData());
                return HookResult.Continue;
            });

            _plugin.RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.ServerDataCollection) return HookResult.Continue;
                CCSPlayerController player = @event.Userid!;
                if (_helper.isValidPlayer(player) == false || player.IsHLTV || player.IsBot)
                    return HookResult.Continue;

                if (@event.Reason != 1)
                {
                    Task.Run(() => SendServerData());
                }
                return HookResult.Continue;
            });

            _plugin.RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.ServerDataCollection) return HookResult.Continue;
                CCSPlayerController player = @event.Userid!;
                if (player == null || _helper.isValidPlayer(player) == false || player!.IsHLTV && player.IsBot)
                    return HookResult.Continue;


                Task.Run(() => SendServerData());
                return HookResult.Continue;
            });

            _plugin.RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.ServerDataCollection) return HookResult.Continue;
                int winner = @event.Winner;
                if (winner == (byte)CsTeam.CounterTerrorist)
                {
                    roundsWinsCt++;

                }
                if (winner == (byte)CsTeam.Terrorist)
                {
                    roundsWinsT++;
                }

                Task.Run(() => SendServerData());
                return HookResult.Continue;
            }, HookMode.Post);


            _plugin.RegisterEventHandler((EventBeginNewMatch @evennt, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.ServerDataCollection) return HookResult.Continue;
                roundsWinsCt = 0;
                roundsWinsT = 0;
                long currentUnixTimestampSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                matchStartTime = currentUnixTimestampSeconds;
                return HookResult.Continue;
            }, HookMode.Post);

            _plugin.RegisterEventHandler((EventWarmupEnd @evennt, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.ServerDataCollection) return HookResult.Continue;
                roundsWinsCt = 0;
                roundsWinsT = 0;
                long currentUnixTimestampSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                matchStartTime = currentUnixTimestampSeconds;
                isWarmupRound = false;
                return HookResult.Continue;
            }, HookMode.Post);


        }

        public async Task SendServerData(bool includePlayers = true)
        {
            if (isRequestInProgress || DateTime.UtcNow - lastRequestTime < requestCooldown)
            {
                return;
            }

            isRequestInProgress = true;
            lastRequestTime = DateTime.UtcNow;

            _plugin = (_pluginContext.Plugin as GameCMSPlugin)!;


            try
            {
                await Server.NextWorldUpdateAsync(async () =>
                {
                    try
                    {
                        var players = Utilities.GetPlayers()
                            .Where(x => x.Connected == PlayerConnectedState.PlayerConnected
                                && _helper.isValidPlayer(x)
                                && !x.IsHLTV
                                && !x.IsBot
                                && x.TeamNum != (byte)CsTeam.Spectator
                                && x.TeamNum != (byte)CsTeam.None
                            );

                        var playerInfos = new List<PlayerInfoEntityServerData>();
                        if (players.Any() && includePlayers)
                        {
                            playerInfos = GetPlayerInfos(players).ToList();
                        }

                        float MaxRoundTime = ConVar.Find("mp_roundtime")?.GetPrimitiveValue<float>() ?? 10f;
                        int MaxRounds = ConVar.Find("mp_maxrounds")?.GetPrimitiveValue<int>() ?? 0;
                        var serverDataEntity = new ServerDataEntity(
                            playerInfos,
                            Server.MapName,
                            _helper.GetServerIp() + ":" + ConVar.Find("hostport")?.GetPrimitiveValue<int>()!.ToString() ?? "27015",
                            Server.MaxPlayers,
                            playerInfos.Count,
                            roundsWinsCt,
                            roundsWinsT,
                            MaxRoundTime,
                            MaxRounds,
                            matchStartTime,
                            isWarmupRound
                        );

                        string serverData = JsonSerializer.Serialize(serverDataEntity);
                        string postUrl = $"{_plugin.API_URI_BASE}/server-data/cs2";
                        string postToken = _plugin.Config.ServerApiKey;

                        var formData = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("data", serverData)
                        };
                        var contentData = new FormUrlEncodedContent(formData);

                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", postToken);

                        try 
                        {
                            var postResponse = await httpClient.PostAsync(postUrl, contentData);
                            string responseContent = await postResponse.Content.ReadAsStringAsync();

                            if (postResponse.StatusCode != HttpStatusCode.OK && postResponse.StatusCode != HttpStatusCode.Created)
                            {
                                _logger.LogError("Failed to send server data. Status: {0}, Response: {1}", postResponse.StatusCode, responseContent);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError("Invalid server data format: {0}", ex.Message);
                        }
                        catch (HttpRequestException ex)
                        {
                            _logger.LogError("API connection error: {0}", ex.Message);
                        }
                        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                        {
                            _logger.LogError("API request timeout");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Unexpected error while sending server data: {0}", ex.Message);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError("HttpRequestException occurred: {0}", ex.Message);
                    }
                    catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                    {
                        _logger.LogError("Request timed out: {0}", ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Unexpected error occurred: {0}", ex.Message);
                    }
                    finally
                    {
                        // Always reset isRequestInProgress at the end of execution
                        isRequestInProgress = false;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error in NextWorldUpdateAsync: {0}", ex.Message);
                isRequestInProgress = false; // Reset here as well
            }
        }


        private IEnumerable<PlayerInfoEntityServerData> GetPlayerInfos(IEnumerable<CCSPlayerController> players)
        {
            return players.Select(player => new PlayerInfoEntityServerData
            {
                name = player.PlayerName,
                steam_id = player.SteamID,
                joined_time = _helper.GetPlayerFromTimeCollection(player.SteamID),

                kills = player.ActionTrackingServices!.MatchStats.Kills,
                headshots = player.ActionTrackingServices!.MatchStats.HeadShotKills,
                deaths = player.ActionTrackingServices!.MatchStats.Deaths,
                score = player.Score,
                ping = player.Ping,

                team = (int)player.TeamNum switch
                {
                    2 => "t",
                    3 => "ct",
                    _ => "unknown"
                }
            });
        }




    }

}