namespace GameCMS
{
    //BIG THANKS TO THE K4-SYSTEM
    //FOR ALLOWING USING THEIRS LOGIC HERE!

    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Plugin;
    using CounterStrikeSharp.API.Modules.Utils;
    using Dapper;
    using Microsoft.Extensions.Logging;
    using MySqlConnector;
    using GameCMS.Models;
    using CounterStrikeSharp.API;

    public class PlayingTimeService
    {
        private Helper _helper;
        private ILogger<PlayingTimeService> _logger;
        private IPluginContext _pluginContext;
        private GameCMSPlugin? _plugin;
        private int _serverId;
        private List<PlayerModel> Players = new List<PlayerModel>();
        //private bool _stopRequested = false;
        DateTime lastRoundStartEventTime = DateTime.MinValue;

        public PlayingTimeService(ILogger<PlayingTimeService> logger, Helper helper, IPluginContext pluginContext)
        {
            _helper = helper;
            _logger = logger;
            _pluginContext = pluginContext;
        }

        public void Start(bool hotReload, int serverId)
        {
            _plugin = (_pluginContext.Plugin as GameCMSPlugin)!;
            _serverId = serverId;
            _logger.LogInformation("Starting playing time service");
            _plugin.RegisterEventHandler((EventPlayerConnectFull @event, GameEventInfo info) =>
            {
                //just in case , if they change it via command ?
                if (!_plugin.Config.services.PlayingTime) return HookResult.Continue;

                CCSPlayerController player = @event.Userid!;
                if (_helper.isValidPlayer(player) == false || player.IsHLTV || player.IsBot)
                    return HookResult.Continue;


                if (Players.Any(p => p._controller == player))
                    return HookResult.Continue;

                PlayerModel playerModel = new PlayerModel(player);
                Task.Run(async () =>
                {
                    _ = LoadPlayerDataAsync(playerModel);
                });
                return HookResult.Continue;
            });

            _plugin.RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
                {
                    if (!_plugin.Config.services.PlayingTime) return HookResult.Continue;

                    PlayerModel? playerModel = GetPlayer(@event.Userid!);
                    if (playerModel is null || !playerModel.IsValid || !playerModel.IsPlayer)
                        return HookResult.Continue;

                    DateTime now = _helper.GetTimeNow();
                    TimeData? playerData = playerModel.TimeData;
                    if (playerData != null)
                    {

                        playerData!.TimeFields[GetFieldForTeam(playerModel._controller.Team)] += (int)(now - playerData.Times["Team"]).TotalSeconds;
                        if (@event.Reason != 1)
                        {
                            Task.Run(() => SavePlayerDataAsync(playerModel));
                        }
                    }
                    return HookResult.Continue;
                });

            _plugin.RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
             {
             if (!_plugin.Config.services.PlayingTime) return HookResult.Continue;

             PlayerModel? playerModel = GetPlayer(@event.Userid!);
             if (playerModel is null || !playerModel.IsValid || !playerModel.IsPlayer)
             {

                 CCSPlayerController player = @event.Userid!;
                 if (_helper.isValidPlayer(player) == false || player.IsHLTV || player.IsBot)
                     return HookResult.Continue;

                 PlayerModel playerModel1 = new PlayerModel(player);
                 _ = Task.Run(async () =>
                 {
                     _ = LoadPlayerDataAsync(playerModel1);
                 });
                 return HookResult.Continue;
             }

             TimeData? playerData = playerModel.TimeData;
             if (playerData is null)
             {
                 return HookResult.Continue;
             }

             DateTime now = _helper.GetTimeNow();
             if ((CsTeam)@event.Oldteam != CsTeam.None)
             {
                 int timeSpent = (int)(now - playerData.Times["Team"]).TotalSeconds;
                 string oldTeam = GetFieldForTeam((CsTeam)@event.Oldteam);
                 playerData.TimeFields[oldTeam] += timeSpent;
             }

             playerData.Times["Team"] = now;
             return HookResult.Continue;
         });


            _plugin.RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
            {
                if (!_plugin.Config.services.PlayingTime) return HookResult.Continue;

                Task.Run(SaveAllPlayersDataAsync);
                return HookResult.Continue;
            }, HookMode.Post);


            _plugin.RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
            {


                return HookResult.Continue;
            });



        }


        private string GetFieldForTeam(CsTeam team)
        {
            return team switch
            {
                CsTeam.Terrorist => "t",
                CsTeam.CounterTerrorist => "ct",
                _ => "spec"
            };
        }

        private PlayerModel? GetPlayer(ulong steamID)
        {
            return Players.ToList().FirstOrDefault(player => player.SteamID == steamID);
        }

        private PlayerModel? GetPlayer(CCSPlayerController playerController)
        {
            return Players.ToList().FirstOrDefault(player => player._controller == playerController);
        }

        private async Task LoadPlayerDataAsync(PlayerModel playerModel)
        {

            var currentUnixTimestamp = _helper.GetTime();
            var todayDate = _helper.GetDate();

            var insertOrUpdateQuery = $@"
                    INSERT INTO gcms_players_times (`steam_id`, `server_id`, `username`, `date`, `times_joined`, `time`)
                    VALUES (@SteamId, @ServerId, @Username, @TodayDate, 1, @TodayUnixTimestamp)
                    ON DUPLICATE KEY UPDATE 
                        `username` = VALUES(`username`),
                        `times_joined` = `times_joined` + 1,
                        `time` = VALUES(`time`);

                    SELECT * FROM gcms_players_times
                    WHERE `steam_id` = @SteamId AND `server_id` = @ServerId AND `date` = @TodayDate;
                ";
            try
            {
                using var connection = await Database.Instance.GetConnection();
                var dynamicParameters = new DynamicParameters(new
                {
                    SteamId = playerModel.SteamID,
                    ServerId = _serverId,
                    Username = playerModel.PlayerName,
                    TodayDate = todayDate,
                    TodayUnixTimestamp = currentUnixTimestamp
                });

                var multiQueryResult = await connection.QueryMultipleAsync(insertOrUpdateQuery, dynamicParameters);
                var playerData = multiQueryResult.ReadFirstOrDefault<dynamic>();

                LoadPlayerRowToCache(playerModel, playerData);

                if (GetPlayer(playerModel.SteamID) == null)
                {
                    Players.Add(playerModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while loading player cache: {ex.Message}");
            }
        }


        private void LoadPlayerRowToCache(PlayerModel playerModel, dynamic? playerData)
        {


            if (playerData != null)
            {
                var rowDictionary = (IDictionary<string, object>)playerData;

                Dictionary<string, int> TimeFields = new Dictionary<string, int>();
                string[] timeFieldNames = { "ct", "t", "spec" };
                foreach (string timeField in timeFieldNames)
                {
                    TimeFields[timeField] = Convert.ToInt32(rowDictionary[timeField]);
                }
                DateTime now = _helper.GetTimeNow();
                playerModel.TimeData = new TimeData
                {
                    TimeFields = TimeFields,
                    Times = new Dictionary<string, DateTime>
                    {
                        { "Team",  now}
                    }
                };
            }
        }

        private async Task SavePlayerDataAsync(PlayerModel playerModel)
        {
            using var connection = await Database.Instance.GetConnection();
            var transaction = await connection.BeginTransactionAsync();
            try
            {
                await ExecuteTimeUpdateAsync(playerModel, transaction);
                await transaction.CommitAsync();
                Players.Remove(playerModel);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"An error occurred while saving all players data: {ex.Message}");
            }


        }

        private async Task ExecuteTimeUpdateAsync(PlayerModel playerModel, MySqlTransaction transaction)
        {

            if (transaction.Connection == null)
                throw new InvalidOperationException("The transaction's connection is null.");

            var currentUnixTimestamp = _helper.GetTime();
            var todayDate = _helper.GetDate();

            string query = $@"INSERT INTO gcms_players_times 
                  (`steam_id`, `username`, `server_id`, `date`, `ct`, `t`, `spec`, `time`)
                  VALUES 
                  (@SteamId, @Username, @ServerId, @TodayDate, @ct, @t, @spec, @TodayUnixTimestamp)
                  ON DUPLICATE KEY UPDATE 
                      `username` = VALUES(`username`),
                      `ct` = VALUES(`ct`),
                      `t` = VALUES(`t`),
                      `spec` = VALUES(`spec`),
                      `time` = VALUES(`time`);";

            var dynamicParameters = new DynamicParameters(new
            {
                SteamId = playerModel.SteamID,
                Username = playerModel.PlayerName,
                ServerId = _serverId,
                TodayDate = todayDate,
                TodayUnixTimestamp = currentUnixTimestamp,
                ct = playerModel.TimeData?.TimeFields["ct"],
                t = playerModel.TimeData?.TimeFields["t"],
                spec = playerModel.TimeData?.TimeFields["spec"]
            });
            try
            {
                await transaction.Connection.ExecuteAsync(query, dynamicParameters, transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while saving player data for {todayDate}: {ex.Message}");
            }
        }

        public async Task SaveAllPlayersDataAsync()
        {
            if (Players.Count == 0)
            {
                _logger.LogError($"Could not Save Players Stats, No players found.");
                return;
            }
            else
            {
                using var connection = await Database.Instance.GetConnection();
                var transaction = await connection.BeginTransactionAsync();

                try
                {
                    var updates = Players.Select(playerModel => new
                    {
                        SteamId = playerModel.SteamID,
                        Username = playerModel.PlayerName,
                        ServerId = _serverId,
                        TodayDate = _helper.GetDate(),
                        TodayUnixTimestamp = _helper.GetTime(),
                        Ct = playerModel.TimeData?.TimeFields["ct"],
                        T = playerModel.TimeData?.TimeFields["t"],
                        Spec = playerModel.TimeData?.TimeFields["spec"]
                    }).ToList();

                    string query = @"INSERT INTO gcms_players_times 
                         (`steam_id`, `username`, `server_id`, `date`, `ct`, `t`, `spec`, `time`)
                         VALUES 
                         (@SteamId, @Username, @ServerId, @TodayDate, @Ct, @T, @Spec, @TodayUnixTimestamp)
                         ON DUPLICATE KEY UPDATE 
                             `username` = VALUES(`username`),
                             `ct` = VALUES(`ct`),
                             `t` = VALUES(`t`),
                             `spec` = VALUES(`spec`),
                             `time` = VALUES(`time`);";

                    var rowsAffected = await connection.ExecuteAsync(query, updates, transaction);
                    await transaction.CommitAsync();
                    // var dataToLog = updates.Select(player => new
                    // {
                    //     player.SteamId,
                    //     player.Username,
                    //     player.ServerId,
                    //     player.TodayDate,
                    //     player.Ct,
                    //     player.T,
                    //     player.Spec,
                    //     player.TodayUnixTimestamp
                    // }).ToList();

                    // var jsonData = System.Text.Json.JsonSerializer.Serialize(dataToLog);

                    // _logger.LogInformation($"Executing query: {query}");
                    // _logger.LogInformation($"Data being sent: {jsonData}");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"An error occurred while saving all players data: {ex.Message}");
                }
            }
        }



        public void LoadAllPlayersCache()
        {

            List<CCSPlayerController> players = Utilities.GetPlayers()
                    .Where(x => x.Connected == PlayerConnectedState.PlayerConnected)
                    .Where(x => _helper.isValidPlayer(x) && !x.IsHLTV && !x.IsBot).ToList();

            if (players.Count == 0) return;



            foreach (var player in players)
            {
                PlayerModel playerModel = new PlayerModel(player);
                if (GetPlayer(player.SteamID) == null)
                {
                    Players.Add(playerModel);
                }

            }
            string combinedQuery = $@"SELECT * FROM `gcms_players_times` 
                WHERE `server_id` = @ServerId 
                AND `date` = @TodayDate
                AND `steam_id` IN (" + string.Join(",", players.Select(player => $"'{player.SteamID}'")) + ");";
            Task.Run(() => LoadAllPlayersCacheAsync(combinedQuery));
        }

        public async Task LoadAllPlayersCacheAsync(string combinedQuery)
        {

            try
            {
                using var connection = await Database.Instance.GetConnection();

                var todayDate = _helper.GetDate();

                var dynamicParameters = new DynamicParameters(new
                {
                    ServerId = _serverId,
                    TodayDate = todayDate
                });
                var rows = await connection.QueryAsync<dynamic>(combinedQuery, dynamicParameters);

                if (rows.Count() == 0) return;

                foreach (var row in rows)
                {
                    ulong.TryParse(row.steam_id, out ulong steamId);
                    PlayerModel player = GetPlayer(steamId)!;
                    if (player == null) continue;
                    LoadPlayerRowToCache(player, row);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred while loading all players cache: {ErrorMessage}", ex.Message);
            }
        }


    }

}