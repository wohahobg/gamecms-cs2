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
    using CounterStrikeSharp.API.Modules.Cvars;

    public sealed partial class GameCMSPlugin : BasePlugin, IPluginConfig<GameCMSConfig>
    {
        public override string ModuleName => "GameCMS.ORG";
        public override string ModuleVersion => "1.0.5";
        public override string ModuleAuthor => "GameCMS.ORG (Wohaho)";
        public override string ModuleDescription => "Plugin that allows you to connect your CS2 Server with the platform. Made with love. Keep it simple - GameCMS.ORG";
        public GameCMSConfig Config { get; set; } = new();
        private readonly HttpClient client = new();
        private int serverId = 0;
        private bool dbConnected = false;
        private Helper _helper;
        private WebstoreService _webStoreService;
        private AdminService _adminService;
        private HttpServerSerivce _httpServer;

        private PlayingTimeService _playingTimeService;

        private string API_URI_BASE = "https://api.gamecms.org/v2";


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
            _webStoreService.ListenForCommands(serverApiKey, API_URI_BASE);
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


        public int GetServerId()
        {
            return serverId;
        }

        private int SetServerId()
        {
            var url = $"{API_URI_BASE}/server";
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
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to get server ID.");
            }
            return 0;
        }


        [ConsoleCommand("css_gcms_store_force")]
        [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/root")]
        public void onCommandStoreForce(CCSPlayerController? player, CommandInfo command)
        {
            _webStoreService.TryToFetchStoreCommands(true);
        }

        [ConsoleCommand("css_gcms_server_verify")]
        [CommandHelper(minArgs: 1, usage: "<server-api-key>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/root")]
        public void onCommandServerVerifyAsync(CCSPlayerController? player, CommandInfo command)
        {
            var moduleFolderName = Path.GetFileName(ModuleDirectory);
            var filePath = _helper.GetFilePath($"configs/plugins");
            filePath = _helper.GetFilePath($"{filePath}/{moduleFolderName}");
            filePath = _helper.GetFilePath($"{filePath}/{moduleFolderName}.json");



            if (!_httpServer.IsHttpServerRunning())
            {
                command.ReplyToCommand("GameCMS is no listening for requiests, please ensure you have entered free and open ServerHttpPort, and you have give your server a restart.");
                return;
            }

            if (!dbConnected)
            {
                command.ReplyToCommand("Database not connected! Please ensure you have connected your database. Before run this command.");
                return;
            }

            string ServerKey = command.GetArg(1);

            if (ServerKey.Length < 64)
            {
                command.ReplyToCommand("Invalid Server API Key! Please ensure you have copied the right Server API Key!");
                return;
            }

            var url = $"{API_URI_BASE}/server-verify/cs2";

            HttpRequestMessage request = _helper.GetServerRequestHeaders(ServerKey);
            request.RequestUri = new Uri(url);
            request.Method = HttpMethod.Post;

            var address = _helper.GetServerIp();
            var port = ConVar.Find("hostport")?.GetPrimitiveValue<int>()!.ToString() ?? "27015"; 
            var httpPort = Config.ServerHttpPort;

            string encryptionKey = ServerKey.Substring(0, 32);
            var formData = new Dictionary<string, string>
                {
                    { "address", address },
                    { "port", port.ToString() },
                    { "httpPort", httpPort.ToString() },
                    { "dbHost", _helper.EncryptString(Config.database.host, encryptionKey) },
                    { "dbPort", _helper.EncryptString(Config.database.port.ToString(), encryptionKey) },
                    { "dbName", _helper.EncryptString(Config.database.name, encryptionKey) },
                    { "dbUsername", _helper.EncryptString(Config.database.username, encryptionKey) },
                    { "dbPassword", _helper.EncryptString(Config.database.password, encryptionKey) }
                };

            request.Content = new FormUrlEncodedContent(formData);
            try
            {
                var responseTask = client.SendAsync(request).ConfigureAwait(false);
                var response = responseTask.GetAwaiter().GetResult();
                var readTask = response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string responseBody = readTask.GetAwaiter().GetResult();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var apiResponse = JsonSerializer.Deserialize<ServerResponseEntity>(responseBody);
                    if (apiResponse != null)
                    {
                        serverId = apiResponse.id;
                        Config.ServerApiKey = ServerKey;
                        string jsonString = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(filePath, jsonString);
                        command.ReplyToCommand($"[GameCMS.ORG] Server verified successfully!");
                    }
                    else
                    {
                        command.ReplyToCommand("[GameCMS.ORG] Server verification failed: Unexpected response from the server.");
                    }
                }
                else
                {

                    var apiResponse = JsonSerializer.Deserialize<ServerVerifyResponseEntity>(responseBody);
                    string message = "[GameCMS.ORG] Server verification failed: Unexpected error occurred";
                    if (apiResponse != null)
                    {
                        message = $"[GameCMS.ORG] {apiResponse.message}";
                    }
                    command.ReplyToCommand(message);
                }
            }
            catch (Exception ex)
            {
                command.ReplyToCommand($"[GameCMS.ORG] Exception occurred during server verification: {ex.Message}");
            }






        }


        [ConsoleCommand("css_gcms_reload_admins")]
        [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        [RequiresPermissions("@css/root")]
        public void onCommandReloadAdmins(CCSPlayerController? player, CommandInfo command)
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
                dbConnected = true;
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



