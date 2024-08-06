namespace GameCMS
{

    using System.Net;
    using System.Text.Json;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using Microsoft.Extensions.Logging;


    public class WebstoreService
    {

        private readonly ILogger<HttpServerSerivce> _logger;
        private readonly Helper _helper;
        private readonly HttpClient client = new HttpClient();
        private System.Threading.Timer _timer;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(60); // Interval of 60 seconds
        private string _serverApiKey = string.Empty;

        public WebstoreService(ILogger<HttpServerSerivce> logger, Helper helper)
        {
            _logger = logger;
            _helper = helper;
        }

        public void ListenForCommands(string ServerApiKey)
        {
            _logger.LogInformation("Start listening for webstore commands");
            _serverApiKey = ServerApiKey;

            var startTimeSpan = TimeSpan.Zero;
            var periodTimeSpan = _interval;
            _timer = new System.Threading.Timer((e) =>
            {
                TryToFetchStoreCommands();
            }, null, startTimeSpan, periodTimeSpan);
        }

        public void TryToFetchStoreCommands(bool manual = false)
        {
            _ = FetchStoreCommands(manual);
        }

        private async Task FetchStoreCommands(bool manual)
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
                    if (manual)
                    {
                        _logger.LogInformation($"No commands to process. For API Key: {_serverApiKey}");
                    }
                    return;
                }

                string responseBody = await response.Content.ReadAsStringAsync();

                var apiResponse = JsonSerializer.Deserialize<CommandsResponseEntity>(responseBody);
                if (apiResponse == null) return;

                List<int> executedCommandIds = new List<int>();

                _logger.LogInformation($"Fetched {apiResponse.data.Count} commands from the server.");

                foreach (var commandData in apiResponse.data)
                {
                    await ProgressStoreCommandsAsync(commandData, executedCommandIds);
                }
                await MarkCommandsAsCompleted(executedCommandIds);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogWarning("The request timed out. Please check your network connection or try again later.");
                // Specifically catching the TaskCanceledException that results from a timeout
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
            }
        }

        private async Task ProgressStoreCommandsAsync(CommandDataEntity commandData, List<int> executedCommandIds)
        {
            // Check if the player must be online
            if (commandData.must_be_online)
            {
                bool isOnline = await CheckIfPlayerOnline(commandData.steam_id);
                if (isOnline)
                {
                    ExecuteStoreCommands(commandData, executedCommandIds);
                }
            }
            else
            {
                ExecuteStoreCommands(commandData, executedCommandIds);
            }
        }

        private void ExecuteStoreCommands(CommandDataEntity commandData, List<int> executedCommandIds)
        {
            foreach (var command in commandData.commands)
            {
                Server.NextWorldUpdate(() => Server.ExecuteCommand(command));
            }
            executedCommandIds.Add(commandData.id);
        }
        private async Task MarkCommandsAsCompleted(List<int> commandIds)
        {
            if (commandIds == null || !commandIds.Any())
                return;

            var url = "https://api.gamecms.org/v2/commands/complete";

            HttpRequestMessage request = _helper.GetServerRequestHeaders(_serverApiKey);
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
        private async Task<bool> CheckIfPlayerOnline(ulong steam_id)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            Server.NextFrame(() =>
            {
                CCSPlayerController? playerController = Utilities.GetPlayerFromSteamId(steam_id);
                bool isOnline = playerController != null;
                tcs.SetResult(isOnline);
            });

            return await tcs.Task;
        }


    }
}