namespace GameCMS
{

    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Web;
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Modules.Cvars;
    using CounterStrikeSharp.API.Modules.Utils;
    using Microsoft.Extensions.Logging;


    public class HttpServerSerivce
    {
        private HttpListener? listener = null;
        private readonly ILogger<HttpServerSerivce> _logger;
        private readonly Helper _helper;

        private string _serverApiKey = string.Empty;
        private bool isRunning = false;
        private readonly object syncLock = new object();

        private delegate Task RouteHandler(HttpListenerContext context);

        private Dictionary<(string, string), RouteHandler>? routeMappings;

        public HttpServerSerivce(ILogger<HttpServerSerivce> logger, Helper helper)
        {
            _helper = helper;
            _logger = logger;
        }

        private async Task HandleGetFile(HttpListenerContext context, string fileIdentifier)
        {
            var (success, filePath, reloadCommand, fileName, statusCode, errorMessage) = FindFile(context, fileIdentifier);

            if (!success)
            {
                await SendJsonResponse(context, new { message = errorMessage }, statusCode);
                return;
            }

            string jsonContent = File.ReadAllText(filePath!);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                await SendJsonResponse(context, new { message = $"The file '{fileName}' at path '{filePath}' is empty." }, 400);
                return;
            }

            if (jsonContent == "[]")
            {
                await SendJsonResponse(context, new { data = new Dictionary<string, object>() }, 200);
                return;
            }

            try
            {
                // Remove comments from the JSON content
                string json = Regex.Replace(jsonContent, @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);

                // Determine the type of JSON structure
                if (json.TrimStart().StartsWith("{"))
                {
                    // Deserialize as an object (Dictionary)
                    var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (jsonObject == null)
                    {
                        await SendJsonResponse(context, new { message = $"The file '{fileName}' contains invalid JSON content. Path: {filePath}" }, 400);
                        return;
                    }
                    await SendJsonResponse(context, new { data = jsonObject }, 200);
                }
                else if (json.TrimStart().StartsWith("["))
                {
                    // Deserialize as an array of objects (List of Dictionaries)
                    var jsonArray = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                    if (jsonArray == null)
                    {
                        await SendJsonResponse(context, new { message = $"The file '{fileName}' contains invalid JSON content. Path: {filePath}" }, 400);
                        return;
                    }
                    await SendJsonResponse(context, new { data = jsonArray }, 200);
                }
                else
                {
                    await SendJsonResponse(context, new { message = $"Unknown JSON structure in '{fileName}'." }, 400);
                }
            }
            catch (JsonException ex)
            {
                await SendJsonResponse(context, new { message = $"Failed to parse JSON format in '{fileName}'. Path: {filePath}, Error {ex.Message}" }, 400);
            }


        }

        private async Task HandleUpdateFile(HttpListenerContext context, string fileIdentifier)
        {

            var (filePath, reloadCommand) = GetFileNamePath(fileIdentifier);
            if (string.IsNullOrEmpty(filePath))
            {
                await SendJsonResponse(context, new { message = $"{fileIdentifier} is not recognized or allowed." }, 400);
                return;
            }
            string fileName = Path.GetFileName(filePath);

            if (!File.Exists(filePath))
            {
                await SendJsonResponse(context, new { message = $"The file for '{fileIdentifier}' ({fileName}) was not found." }, 404);
                return;
            }


            using (var reader = new StreamReader(context.Request.InputStream))
            {
                string content = await reader.ReadToEndAsync();
                var formData = HttpUtility.ParseQueryString(content);

                string jsonString = formData["data"]!;

                if (string.IsNullOrEmpty(jsonString))
                {
                    await SendJsonResponse(context, new { message = "No JSON data provided." }, 400);
                    return;
                }

                try
                {

                    var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonString);
                    string cleanJson = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });

                    await File.WriteAllTextAsync(filePath, cleanJson);
                    if (!string.IsNullOrEmpty(reloadCommand))
                    {
                        Server.NextWorldUpdate(() => Server.ExecuteCommand(reloadCommand));
                    }

                    await SendJsonResponse(context, new { message = $"{fileName} updated successfully." }, 200);
                }
                catch (JsonException)
                {
                    await SendJsonResponse(context, new { message = "Invalid JSON data provided." }, 400);
                }
            }
        }

        private async Task HandleCheckConnection(HttpListenerContext context)
        {
            await SendJsonResponse(context, new { message = "Connected!" }, 200);
            return;
        }

        public async Task HandleGetFileKeys(HttpListenerContext context, string fileIdentifier)
        {

            var (success, filePath, reloadCommand, fileName, statusCode, errorMessage) = FindFile(context, fileIdentifier);

            if (!success)
            {
                await SendJsonResponse(context, new { message = errorMessage }, statusCode);
                return;
            }

            string jsonContent = File.ReadAllText(filePath!);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                await SendJsonResponse(context, new { message = $"The file '{fileName}' at path '{filePath}' is empty." }, 400);
                return;
            }

            try
            {
                string content = await File.ReadAllTextAsync(filePath!);

                var contentDict = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

                if (contentDict == null || contentDict.Count == 0)
                {
                    await SendJsonResponse(context, new { message = $"No {fileName} found in the configuration." }, 404);
                    return;
                }

                var groupNames = contentDict.Keys.ToList();

                await SendJsonResponse(context, new { data = groupNames }, 200);
            }
            catch (JsonException)
            {
                await SendJsonResponse(context, new { message = $"Error parsing the {fileName} configuration file." }, 500);
            }
            catch (Exception ex)
            {
                await SendJsonResponse(context, new { message = $"Unexpected error: {ex.Message}" }, 500);
            }
        }

        private async Task HandleCommandsRoute(HttpListenerContext context)
        {
            NameValueCollection formData;
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                string content = await reader.ReadToEndAsync();
                formData = HttpUtility.ParseQueryString(content);
            }

            string command = formData["command"]!;

            if (string.IsNullOrEmpty(command))
            {
                await SendJsonResponse(context, new { message = "Command not provided." }, 400);
                return;
            }

            Server.NextWorldUpdate(() => Server.ExecuteCommand(command));
            await SendJsonResponse(context, new { message = "Command processed" });
        }

        private async Task HandlePluginsListRoute(HttpListenerContext context)
        {

        }

        private async Task HandleGetTimezone(HttpListenerContext context)
        {
            TimeZoneInfo localTimeZone = TimeZoneInfo.Local;
            String TimeZoneId = localTimeZone.Id;
            await SendJsonResponse(context, new { timezone = TimeZoneId });
        }

        private async Task HandlePlayersRoute(HttpListenerContext context)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            var queryString = context.Request.QueryString;
            bool includeBots = false;
            ulong playerSteamId = 0;


            if (ulong.TryParse(queryString["steam_id"], out ulong steamId))
            {
                playerSteamId = steamId;
            }

            if (bool.TryParse(queryString["bots"], out bool botsParam))
            {
                includeBots = botsParam;
            }

            Server.NextWorldUpdate(async () =>
            {
                var players = Utilities.GetPlayers()
                    .Where(x => x.Connected == PlayerConnectedState.PlayerConnected);

                // Optionally filter out bots based on the "bots" parameter.
                if (!includeBots)
                {
                    players = players.Where(x => !x.IsBot);
                }

                if (playerSteamId != 0)
                {
                    players = players.Where(x => x.SteamID == playerSteamId);
                }

                var playerInfos = GetPlayerInfos(players);
                await SendJsonResponse(context, playerInfos);
                tcs.SetResult(true);
            });

            await tcs.Task;
        }

        private IEnumerable<PlayerInfoEntity> GetPlayerInfos(IEnumerable<CCSPlayerController> players)
        {
            return players.Select(player => new PlayerInfoEntity
            {
                user_id = player.UserId,
                name = player.PlayerName,
                steam_id = player.SteamID,
                account_id = player.AuthorizedSteamID?.AccountId.ToString(),
                ip_address = player.IpAddress,
                joined_time = _helper.GetPlayerFromTimeCollection(player.SteamID),
                is_bot = player.IsBot,
                kills = player.Kills.Count,
                deaths = player.ActionTrackingServices!.MatchStats.Deaths,
                score = player.Score,

                ping = player.Ping,
                is_spectator = player.TeamNum == (byte)CsTeam.Spectator,
                is_terrorist = player.TeamNum == (byte)CsTeam.Terrorist,
                is_counter_terrorist = player.TeamNum == (byte)CsTeam.CounterTerrorist,
            });
        }

        public async Task SendJsonResponse(HttpListenerContext context, object responseObject, int statusCode = 200)
        {
            try
            {
                string jsonResponse = JsonSerializer.Serialize(responseObject);
                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (ObjectDisposedException ex)
            {
                // Log the error or handle it as necessary
                _logger.LogWarning($"Attempted to write to a disposed HttpListenerResponse. Error: {ex.Message}");
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private (bool success, string? filePath, string? reloadCommand, string? fileName, int statusCode, string message) FindFile(HttpListenerContext context, string fileIdentifier)
        {
            var (filePath, reloadCommand) = GetFileNamePath(fileIdentifier);
            string? fileName = null;
            string errorMessage = "";
            int statusCode = 200;
            if (string.IsNullOrEmpty(filePath))
            {
                errorMessage = $"{fileIdentifier} is not recognized or allowed.";
                statusCode = 400;
            }
            else
            {
                fileName = Path.GetFileName(filePath);

                if (!File.Exists(filePath))
                {
                    errorMessage = $"The file for '{fileIdentifier}' ({fileName}) was not found.";
                    statusCode = 404;
                }
            }
            if (statusCode != 200)
            {
                return (false, null, null, null, statusCode, errorMessage);
            }
            return (true, filePath, reloadCommand, fileName, statusCode, "File found successfully.");
        }

        private (string? filePath, string? reloadCommand) GetFileNamePath(string file)
        {
            var result = file switch
            {
                "VipCoreGroups" => (_helper.GetFilePath("configs/plugins/VIPCore/vip.json"), "css_vip_reload"),
                "AdminGroups" => (_helper.GetFilePath("configs/admin_groups.json"), "css_groups_reload"),
                "AdminOverrides" => (_helper.GetFilePath("configs/admin_overrides.json"), "css_overrides_reload"),
                "Admins" => (_helper.GetFilePath("configs/admins.json"), "css_admins_reload"),
                "K4-Zenith-Ranks" => (_helper.GetFilePath("plugins/K4-Zenith-Ranks/ranks.jsonc"), null),
                _ => (null, null), // Handle the default case by returning nulls or some default value
            };
            return result;
        }

        private async Task RouteRequest(HttpListenerContext context)
        {
            if (context.Request?.Url == null)
            {
                await SendJsonResponse(context, new { message = "Route not found!" }, 404);
                return;
            }

            string url = context.Request.Url.AbsolutePath.ToLower();
            string method = context.Request.HttpMethod;

            // Check authorization for routes other than /check-connection
            if (!url.Equals("/check-connection") && !IsAuthorized(context))
            {
                await SendJsonResponse(context, new { message = "Unauthorized" }, 401);
                return;
            }

            // Attempt to find and invoke the handler for the route
            if (routeMappings!.TryGetValue((url, method), out var handler))
            {
                await handler(context);
            }
            else
            {
                await SendJsonResponse(context, new { message = "Route Not Found" }, 404);
            }
        }

        private bool IsAuthorized(HttpListenerContext context)
        {
            string authHeader = context.Request.Headers["Authorization"]!;
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return false;
            }

            string token = authHeader.Substring("Bearer ".Length).Trim();
            return token == _serverApiKey;
        }

        public void SetServerApiKey(string ServerApiKey)
        {
            _serverApiKey = ServerApiKey;
        }

        public void Start(int port, string serverApiKey)
        {
            _serverApiKey = serverApiKey;
            lock (syncLock)
            {
                if (isRunning)
                {
                    _logger.LogInformation("Server is already running.");
                    return;
                }

                SetRoutings();
                string localIP = ConVar.Find("ip")!.StringValue;
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://*:{port}/");
                    listener.Start();
                    isRunning = true;

                    _logger.LogInformation($"Server started, listening for requests on: {localIP}:{port}");
                    Listen();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to start server: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            lock (syncLock)
            {
                if (!isRunning)
                {
                    _logger.LogInformation("Server is not running.");
                    return;
                }

                if (listener != null)
                {
                    listener.Stop();
                    listener.Close();
                    listener = null;
                }
                isRunning = false;
            }
        }

        private async void Listen()
        {
            while (isRunning && listener != null)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    await RouteRequest(context);
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("Listener has been disposed.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error: {ex.Message}");
                    if (!isRunning) break;
                }
            }
        }

        public Helper getHelper()
        {
            return _helper;
        }

        private void SetRoutings()
        {
            routeMappings = new Dictionary<(string, string), RouteHandler>
            {
                {("/check-connection", "GET"), HandleCheckConnection},
                {("/command", "POST"), HandleCommandsRoute},
                {("/players", "GET"), HandlePlayersRoute},
                {("/plugins", "GET"), HandlePluginsListRoute},

                {("/main-configs/admins", "GET"), (context) => HandleGetFile(context, "Admins")},
                {("/main-configs/admins", "POST"), (context) => HandleUpdateFile(context, "Admins") },

    
                //this will return the file that content the groups.
                {("/main-configs/admins/groups", "GET"), (context) => HandleGetFile(context, "AdminGroups")},
                 //this will retrun just list wih groups names.
                {("/main-configs/admins/groups/list", "GET"), (context) => HandleGetFileKeys(context, "AdminGroups")},
                {("/main-configs/admins/groups", "POST"), (context) => HandleUpdateFile(context, "AdminGroups") },

                {("/main-configs/admins/overrides", "GET"), (context) => HandleGetFile(context, "AdminOverrides")},
                {("/main-configs/admins/overrides/list", "GET"), (context) => HandleGetFileKeys(context, "AdminOverrides")},
                {("/main-configs/admins/overrides", "POST"), (context) => HandleUpdateFile(context, "AdminOverrides") },

                {("/k4-zenith-ranks", "GET"), (context) => HandleGetFile(context, "K4-Zenith-Ranks") },

                {("/timezone", "GET"), HandleGetTimezone},

                {("/vipcore/groups", "GET"),  (context) => HandleGetFile(context, "VipCoreGroups") },
                {("/vipcore/groups", "POST"), (context) => HandleUpdateFile(context, "VipCoreGroups") },
                {("/vipcore/server", "GET"), (context) => new VIPCoreHelper(this).GetVipCoreServer(context)},


                {("/vip-plugin/groups", "GET"),  (context) => new VIPPluginHelper(this).GetVIPGroups(context)},
            };
        }

        public bool IsHttpServerRunning()
        {
            return isRunning;
        }
    }
}