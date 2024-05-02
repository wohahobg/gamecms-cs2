namespace GameCMS
{

	using System.Net.Http.Headers;
	using System.Text.Json;
	using System.Text.RegularExpressions;
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;


	public class Helper
	{

		private string _directory = string.Empty;
		private Dictionary<ulong, long> _playersTimeCollection = new Dictionary<ulong, long>();

		public void AddPlayerToTimeCollection(ulong steam_id)
		{
			long currentUnixTimestampSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			_playersTimeCollection[steam_id] = currentUnixTimestampSeconds;
		}

		public long GetPlayerFromTimeCollection(ulong steam_id)
		{
			_playersTimeCollection.TryGetValue(steam_id, out long timestamp);
			return timestamp;
		}

		public void setDirecotry(string directory)
		{
			_directory = directory;
		}

		public List<CCSPlayerController> GetPlayerFromName(string name)
		{
			return Utilities.GetPlayers().FindAll(x => x.PlayerName.Contains(name, StringComparison.OrdinalIgnoreCase));
		}

		public List<CCSPlayerController> GetPlayerFromSteamid64(string steamid)
		{
			return Utilities.GetPlayers().FindAll(x =>
				x.AuthorizedSteamID != null &&
				x.AuthorizedSteamID.SteamId64.ToString().Equals(steamid, StringComparison.OrdinalIgnoreCase)
			);
		}

		public List<CCSPlayerController> GetPlayerFromIp(string ipAddress)
		{
			return Utilities.GetPlayers().FindAll(x =>
				x.IpAddress != null &&
				x.IpAddress.Split(":")[0].Equals(ipAddress)
			);
		}

		public bool IsValidSteamID64(string input)
		{
			string pattern = @"^\d{17}$";

			return Regex.IsMatch(input, pattern);
		}

		public long CalculateExpiryInSeconds(long seconds)
		{
			//if is 0 means no time?
			if (seconds == 0) return 0;
			DateTime now = DateTime.UtcNow;
			DateTime futureTime = now.AddSeconds(seconds);
			long unixTime = ((DateTimeOffset)futureTime).ToUnixTimeSeconds();
			return unixTime;
		}

		public long GetTime()
		{
			return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
		}

		public DateTime GetDate()
		{
			return DateTime.UtcNow.Date;
		}

		public DateTime GetTimeNow()
		{
			return DateTime.UtcNow;
		}

		public HttpRequestMessage GetServerRequestHeaders(string serverApiKey)
		{
			var request = new HttpRequestMessage();
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serverApiKey);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			return request;
		}

		public string[] DeserializeJsonStringArray(string json)
		{
			if (string.IsNullOrWhiteSpace(json) || json == "{}")
			{
				return new string[0]; // Return an empty array if JSON is an empty object or null/whitespace
			}

			try
			{
				return JsonSerializer.Deserialize<string[]>(json) ?? new string[0];
			}
			catch (JsonException ex)
			{
				Console.WriteLine($"JSON deserialization error: {ex.Message}");
				return new string[0]; // Return an empty array in case of deserialization error
			}
		}

		public string GetFilePath(string path)
		{
			string baseDirectory = Path.Combine(_directory, "csgo/addons/counterstrikesharp");
			string combinePath = Path.Combine(baseDirectory, path);
			return Path.GetFullPath(combinePath);
		}
	}
}