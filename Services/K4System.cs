using System.Text.Json;
using System.Text.RegularExpressions;
using Main;

namespace Services;

public class K4System
{
    public static async Task SyncRanks(int serverId, Helper _helper)
    {
        try
        {
            string query = "DELETE FROM gcms_admins WHERE server_id = @server_id";
            var parameters = new Dictionary<string, object> { { "@server_id", serverId } };
            await Database.Instance.Delete(query, parameters);

            var ranksFilePath = _helper.GetFilePath("plugins/K4-System/ranks.jsonc");
            var jsonContent = Regex.Replace(File.ReadAllText(ranksFilePath), @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);
            var rankDictionary = JsonSerializer.Deserialize<Dictionary<string, Entities.K4SystemRank>>(jsonContent);

            if (rankDictionary == null) return;

            foreach (var rank in rankDictionary.Values)
            {
                await Database.Instance.Insert("gcms_k4systemranks", rank);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Task threw an exception: {ex.Message}");

        }
    }
}