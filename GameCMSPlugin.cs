namespace GameCMS
{

    using CounterStrikeSharp.API.Core;
    using System.Text.Json;
    using System.Net;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Modules.Commands;
    using CounterStrikeSharp.API.Core.Attributes.Registration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using static CounterStrikeSharp.API.Core.Listeners;
    using CounterStrikeSharp.API.Modules.Admin;
    using System.Text.RegularExpressions;
    using MySqlConnector;
    using Dapper;

    public sealed partial class GameCMSPlugin : BasePlugin, IPluginConfig<GameCMSConfig>
    {
        public override string ModuleName => "GameCMS.ORG";
        public override string ModuleVersion => "1.0.4";
        public override string ModuleAuthor => "GameCMS.ORG (Wohaho)";
        public override string ModuleDescription => "Plugin that allows you to connect your CS2 Server with the platform. Made with love. Keep it simple - GameCMS.ORG";
        public GameCMSConfig Config { get; set; } = new();
        private readonly HttpClient client = new();
        private int serverId = 0;
        private Helper _helper;
        private WebstoreService _webStoreService;
        private AdminService _adminService;
        private HttpServerSerivce _httpServer;

        private PlayingTimeService _playingTimeService;


        public GameCMSPlugin(Helper helper, WebstoreService webstoreService, AdminService adminService, HttpServerSerivce httpServer, PlayingTimeService playingTimeService)
        {
            _helper = helper;
            _helper.setDirecotry(Server.GameDirectory);
            _adminService = adminService;
            _httpServer = httpServer;
            _webStoreService = webstoreService;
            _playingTimeService = playingTimeService;
        }

        public override void Load(bool hotReload)
        {
            int ServerHttpPort = Config.ServerHttpPort;
            string serverApiKey = Config.ServerApiKey;
            _httpServer.Start(ServerHttpPort, serverApiKey);
            _webStoreService.ListenForCommands(serverApiKey);
            _playingTimeService.Start(hotReload, serverId);

            //load all player cache.

            //RegisterListener<OnMapStart>(OnMapStart);
            if (hotReload)
            {
                _playingTimeService.LoadAllPlayersCache();
            }

        }

        public override void Unload(bool hotReload)
        {
            _httpServer.Stop();

            Task.Run(() => _playingTimeService.SaveAllPlayersDataAsync());

        }

        private void OnMapStart(string OnMapStart)
        {
            //serverId = GetServerId();
            // _adminService.ProgressAdminsData(serverId, Config.DeleteExpiredAdmins);
        }

        public int GetServerId()
        {
            return serverId;
        }

        private int SetServerId()
        {
            var url = "https://api.gamecms.org/v2/cs2/server";
            HttpRequestMessage request = _helper.GetServerRequestHeaders(Config.ServerApiKey);
            request.RequestUri = new Uri(url);
            request.Method = HttpMethod.Get;
            try
            {
                var responseTask = client.SendAsync(request).ConfigureAwait(false);
                var response = responseTask.GetAwaiter().GetResult();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var readTask = response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string responseBody = readTask.GetAwaiter().GetResult();
                    var apiResponse = JsonSerializer.Deserialize<ServerResponseEntity>(responseBody);
                    if (apiResponse != null)
                    {
                        Logger.LogInformation("Successfully connected to GameCMS API.");
                        return apiResponse.id;
                    }
                }
                Logger.LogWarning("Could not connect to GameCMS API Please make sure your Server API key is set correctly.");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Logger.LogWarning("The request timed out. Please check your network connection or try again later.");
                // Specifically catching the TaskCanceledException that results from a timeout
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to get server ID.");
            }
            return 0; // Return 0 in case of any exceptions, including timeout
        }


        [ConsoleCommand("css_gcms_force_store")]
        [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/root")]
        public void OnCMSForceStoreCommands(CCSPlayerController? player, CommandInfo command)
        {
            _webStoreService.TryToFetchStoreCommands(true);
        }


        [ConsoleCommand("css_gcms_reload_admins")]
        [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/root")]
        public void OnCMSReloadAdmins(CCSPlayerController? player, CommandInfo command)
        {
            _adminService.ProgressAdminsData(serverId, Config.DeleteExpiredAdmins);
        }

        [ConsoleCommand("css_gcms_k4syncranks")]
        [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/root")]
        public void OnCommandSyncK4SystemRank(CCSPlayerController? player, CommandInfo command)
        {

            if (serverId == 0)
            {
                command.ReplyToCommand("[GameCMS.ORG] Unable to locate the Server ID. Please ensure that your plugin is successfully connected to GameCMS.ORG by reloading the plugin. If the issue persists, verify your configuration settings or contact support for assistance.");
                return;
            }
            _ = Task.Run(async () =>
            {
                async Task TaskSync()
                {
                    await using MySqlConnection connection = await Database.Instance.GetConnection();
                    try
                    {
                        string query = "DELETE FROM gcms_k4systemranks WHERE server_id = @server_id";
                        await connection.ExecuteAsync(query, new { server_id = serverId });

                        var ranksFilePath = _helper.GetFilePath("plugins/K4-System/ranks.jsonc");
                        var jsonContent = Regex.Replace(File.ReadAllText(ranksFilePath), @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);
                        var rankDictionary = JsonSerializer.Deserialize<Dictionary<string, K4SystemRankEntity>>(jsonContent);

                        if (rankDictionary == null) return;

                        foreach (var rank in rankDictionary.Values)
                        {
                            rank.server_id = serverId;
                            await Database.Instance.Insert("gcms_k4systemranks", connection, rank);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"K4System rank sync threw an error: {ex.Message}");

                    }
                }
                await TaskSync();
            });
            command.ReplyToCommand("[GameCMS.ORG] Ranks have been synced successfully");
        }

        [GameEventHandler]
        public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;

            if (player == null || string.IsNullOrEmpty(player.IpAddress) || player.IpAddress.Contains("127.0.0.1")
                || player.IsBot || player.IsHLTV || !player.UserId.HasValue)
                return HookResult.Continue;

            _helper.AddPlayerToTimeCollection(player.SteamID);

            return HookResult.Continue;
        }

        public void OnConfigParsed(GameCMSConfig config)
        {
            Config = config;

            serverId = SetServerId();

            try
            {
                Database.Initialize(config, Logger);
                Logger.LogInformation("Connected to the database.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Database error: {ex.Message}");
            }
            Task.Run(async () =>
            {
                try
                {
                    await Database.Instance.CreateTableAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Database error: {ex.Message}");
                }
            });

        }
    }


    public class PluginServices : IPluginServiceCollection<GameCMSPlugin>
    {
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<Helper>();
            serviceCollection.AddSingleton<WebstoreService>();
            serviceCollection.AddSingleton<AdminService>();
            serviceCollection.AddSingleton<HttpServerSerivce>();
            serviceCollection.AddSingleton<PlayingTimeService>();
        }
    }

}



