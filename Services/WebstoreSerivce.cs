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
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(60); // Interval of 60 seconds
        private Timer? _timer = null;
        private string _serverApiKey = string.Empty;

        private string API_URI_BASE = string.Empty;

        public WebstoreService(ILogger<HttpServerSerivce> logger, Helper helper)
        {
            _logger = logger;
            _helper = helper;
        }


        public void SetServerApiKey(string ServerApiKey)
        {
            _serverApiKey = ServerApiKey;
        }

        public void ListenForCommands(string ServerApiKey, string API_URI_BASE)
        {
            _logger.LogInformation("Start listening for webstore commands");
            _serverApiKey = ServerApiKey;
            this.API_URI_BASE = $"{API_URI_BASE}/commands";

            var startTimeSpan = TimeSpan.Zero;
            var periodTimeSpan = _interval;

            _timer = new Timer((e) =>
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
            var url = $"{API_URI_BASE}/queue/cs2";
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

            var url = $"{API_URI_BASE}/complete";

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