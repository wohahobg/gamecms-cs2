using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Entities;
using Main;
using Microsoft.Extensions.Logging;
using Helpers;
using System.Net.Http.Headers;

namespace Services;
public class WebstoreService
{

    private readonly ILogger<HttpServerSerivce> _logger;
    private readonly Helper _helper;
    private Timer? _timer;
    private readonly HttpClient client = new HttpClient();
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(60); // Interval of 60 seconds
    private string _serverApiKey = string.Empty;

    public WebstoreService(ILogger<HttpServerSerivce> logger, Helper helper)
    {
        _logger = logger;
        _helper = helper;
    }


    public void ListenForCommands(string ServerApiKey)
    {


        _logger.LogInformation("Start lising for websotre commands");
        _serverApiKey = ServerApiKey;
        var startTimeSpan = TimeSpan.Zero;
        var periodTimeSpan = TimeSpan.FromMinutes(1);
        var timer = new System.Threading.Timer((e) =>
        {
            TryToFetchStoreCommands();
        }, null, startTimeSpan, periodTimeSpan);

    }

    public void TryToFetchStoreCommands()
    {
        _ = FetchStoreCommands();
    }

    private async Task FetchStoreCommands()
    {
        var url = "https://api.gamecms.org/v2/commands/queue/cs2";
        HttpRequestMessage request = _helper.GetServerRequestHeaders(_serverApiKey);
        request.RequestUri = new Uri(url);
        request.Method = HttpMethod.Get;
        try
        {
            var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return;
            }

            string responseBody = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonSerializer.Deserialize<CommandsResponseEntity>(responseBody);
            if (apiResponse == null) return;

            List<int> executedCommandIds = new List<int>();

            foreach (var commandData in apiResponse.data)
            {
                ProgressStoreCommands(commandData, executedCommandIds);
            }
            await MarkCommandsAsCompleted(executedCommandIds);

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);
        }
    }

    private void ProgressStoreCommands(CommandDataEntity commandData, List<int> executedCommandIds)
    {
        // Check if the player must be online
        if (commandData.must_be_online)
        {
            CheckIfPlayerOnline(commandData.steam_id, isOnline =>
            {
                if (isOnline)
                {
                    ExecuteStoreCommands(commandData);
                    executedCommandIds.Add(commandData.id);
                }
            });
        }
        else
        {
            ExecuteStoreCommands(commandData);
            executedCommandIds.Add(commandData.id);
        }
    }

    private void ExecuteStoreCommands(CommandDataEntity commandData)
    {
        foreach (var command in commandData.commands)
        {
            Server.NextWorldUpdate(() => Server.ExecuteCommand(command));
        }
    }
    private async Task MarkCommandsAsCompleted(List<int> commandIds)
    {
        if (commandIds == null || !commandIds.Any())
            return;

        var url = "https://api.gamecms.org/v2/commands/complete";

        HttpRequestMessage request =_helper.GetServerRequestHeaders(_serverApiKey);
        request.RequestUri = new Uri(url);
        request.Method = HttpMethod.Post;

        var jsonContent = JsonSerializer.Serialize(commandIds);

        var formData = new Dictionary<string, string> { { "ids", jsonContent } };

        request.Content = new FormUrlEncodedContent(formData);

        try
        {
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed to mark commands as completed: " + response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception in MarkCommandsAsCompleted: " + ex.Message);
        }
    }

    private void CheckIfPlayerOnline(ulong steam_id, Action<bool> callback)
    {

        Server.NextFrame(() =>
        {
            CCSPlayerController? playerController = Utilities.GetPlayerFromSteamId(steam_id);
            bool isOnline = playerController != null;

            callback(isOnline);
        });
    }



}