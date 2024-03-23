using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Net;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;
using Entities;
using Services;
using Microsoft.Extensions.DependencyInjection;
using static CounterStrikeSharp.API.Core.Listeners;
using System.Reflection;
using CounterStrikeSharp.API.Modules.Admin;
using Serilog;

namespace Main;
[MinimumApiVersion(159)]
public partial class GameCMSPlugin : BasePlugin, IPluginConfig<GameCMSConfig>
{
    public override string ModuleName => "GameCMS.ORG Plugin";
    public override string ModuleVersion => "1.0.0";

    public GameCMSConfig Config { get; set; } = new();

    private Timer? _timer;
    private readonly HttpClient client = new HttpClient();
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(60); // Interval of 60 seconds
    private int serverId;
    private Helper _helper;
    private WebstoreService _webStoreService;
    private AdminService _adminService;
    private HttpServerSerivce _httpServer;

    public GameCMSPlugin(Helper helper,WebstoreService webstoreService, AdminService adminService, HttpServerSerivce httpServer)
    {
        _helper = helper;
        _helper.setDirecotry(Server.GameDirectory);
        _adminService = adminService;
        _httpServer = httpServer;
        _webStoreService = webstoreService;
    }

    public override void Load(bool hotReload)
    {
        int ServerHttpPort = Config.ServerHttpPort;
        string serverApiKey = Config.ServerApiKey;
        _httpServer.Start(ServerHttpPort, serverApiKey);
        _webStoreService.ListenForCommands(serverApiKey);
        RegisterListener<OnMapStart>(OnMapStart);
    }

    public override void Unload(bool hotReload)
    {
        _httpServer.Stop();
    }



    private void OnMapStart(string OnMapStart)
    {
        _adminService.ProgressAdminsData(serverId, Config.DeleteExpiredAdmins);
    }

    private int getServerId()
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
                    //Run Task that
                    return apiResponse.id;
                }
            }
            Logger.LogWarning("Could not connect to GameCMS API Please make sure your Server API key is set correctly.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get server ID.");
        }
        return 0;
    }

    [ConsoleCommand("css_gcms_reload_admins")]
    [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    [RequiresPermissions("@css/root")]
    public void OnCommandReloadAdmins(CCSPlayerController? player, CommandInfo command)
    {
        _adminService.ProgressAdminsData(serverId, Config.DeleteExpiredAdmins);
    }

    private void TryToFetchStoreCommands(object? state)
    {
        
    }



    public void OnConfigParsed(GameCMSConfig config)
    {
        Config = config;

        serverId = getServerId();

        try
        {
            Database.Initialize(config);
            Logger.LogInformation("Connected to the database.");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Database error: {ex.Message}");
        }
        Task.Run(() =>
        {
            try
            {
                _ = Database.Instance.CreateTableAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Database error: {ex.Message}");
            }
        });

        try
        {
            var moduleFolderName = Path.GetFileName(ModuleDirectory);
            var filePath = Path.GetFullPath(Path.Combine(Server.GameDirectory, $"csgo/addons/counterstrikesharp/configs/plugins/{moduleFolderName}/{moduleFolderName}.json"));
            var jsonContent = File.ReadAllText(filePath);

            var currentConfigDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
            var defaultConfigDict = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(config));

            var missingKeys = defaultConfigDict!.Keys.Except(currentConfigDict!.Keys).Any();
            var extraKeys = currentConfigDict.Keys.Except(defaultConfigDict.Keys).Any();

            bool needsUpdate = missingKeys || extraKeys;
            if (needsUpdate)
            {
                var updatedJsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }); // Serialize the default config as the updated content
                File.WriteAllText(filePath, updatedJsonContent);
            }
        }
        catch (JsonException)
        {
            // Handle invalid JSON (e.g., log the error or notify the user)
        }
        catch (Exception)
        {
            // Handle other exceptions
        }

    }
}

public class GameCMSPluginServiceCollection : IPluginServiceCollection<GameCMSPlugin>
{
    public void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<Helper>();
        serviceCollection.AddSingleton<WebstoreService>();
        serviceCollection.AddSingleton<AdminService>();
        serviceCollection.AddSingleton<HttpServerSerivce>();
    }
}

