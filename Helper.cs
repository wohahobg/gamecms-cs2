namespace GameCMS
{


	using System;
	using System.Security.Cryptography;
	using System.Text;
	using System.Net.Http.Headers;
	using System.Runtime.InteropServices;
	using System.Text.Json;
	using System.Text.RegularExpressions;
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;


	public class Helper
	{

		private string _directory = string.Empty;

		public delegate nint CNetworkSystem_UpdatePublicIp(nint a1);
		public static CNetworkSystem_UpdatePublicIp? _networkSystemUpdatePublicIp;


		private Dictionary<ulong, long> _playersTimeCollection = new Dictionary<ulong, long>();


		// This method is taken from 
		//https://github.com/daffyyyy/CS2-SimpleAdmin/blob/main/Helper.cs
		//thanks to daffyyyy
		public string GetServerIp()
		{
			var networkSystem = NativeAPI.GetValveInterface(0, "NetworkSystemVersion001");

			unsafe
			{
				if (_networkSystemUpdatePublicIp == null)
				{
					var funcPtr = *(nint*)(*(nint*)(networkSystem) + 256);
					_networkSystemUpdatePublicIp = Marshal.GetDelegateForFunctionPointer<CNetworkSystem_UpdatePublicIp>(funcPtr);
				}
				/*
				struct netadr_t
				{
				   uint32_t typeJust read the wiki, i have spent tons of times to do it.
				   uint8_t ip[4]
				   uint16_t port
				}
				*/
				// + 4 to skip type, because the size of uint32_t is 4 bytes
				var ipBytes = (byte*)(_networkSystemUpdatePublicIp(networkSystem) + 4);
				// port is always 0, use the one from convar "hostport"
				return $"{ipBytes[0]}.{ipBytes[1]}.{ipBytes[2]}.{ipBytes[3]}";
			}
		}


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

		public string EncryptString(string text, string key)
		{
			// Ensure the key is 32 bytes long for AES-256
			using (Aes aes = Aes.Create())
			{
				aes.Key = Encoding.UTF8.GetBytes(key.Substring(0, 32));  // Use first 32 bytes (64 hex characters)
				aes.GenerateIV();
				aes.Mode = CipherMode.CBC;

				ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

				using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
				{
					ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV to the ciphertext
					using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
					{
						using (StreamWriter sw = new StreamWriter(cs))
						{
							sw.Write(text);
						}
					}

					return Convert.ToBase64String(ms.ToArray());
				}
			}
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

        public bool isValidPlayer(CCSPlayerController player)
        {
             return player != null &&
               player.IsValid &&
               player.PlayerPawn.IsValid &&
               player.AuthorizedSteamID != null;
        }
    }
}