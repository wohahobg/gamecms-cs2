using System.Data;
using System.Text.Json;
using CounterStrikeSharp.API;
using Entities;
using Main;
using Microsoft.Extensions.Logging;

namespace Services;
public class AdminService
{
    private readonly ILogger<AdminService> _logger;
    private readonly Helper _helper;

    public AdminService(ILogger<AdminService> logger, Helper helper)
    {
        _helper = helper;
        _logger = logger;
    }

    public void ProgressAdminsData(int serverId, bool deleteExpiredAdmins)
    {

        Task.Run(async () =>
        {

            if (deleteExpiredAdmins)
            {
                await DeleteExpiredAdminsAsync(serverId);
            }

            try
            {

                var admins = await FetchAllAdmins(serverId);
                var groups = await FetchAllGroups(serverId);
                var overrides = await FetchAllOverrides(serverId);

                var adminsDict = new Dictionary<string, object>();
                var groupsDict = new Dictionary<string, object>();
                var overridesDict = new Dictionary<string, object>();

                if (groups.Any())
                {
                    foreach (var group in groups)
                    {
                        Console.WriteLine(group.immunity);
                        groupsDict[group.name] = new
                        {
                            immunity = group.immunity,
                            flags = JsonSerializer.Deserialize<List<string>>(group.flags) ?? new List<string>(),
                        };
                    }
                }

                if (overrides.Any())
                {
                    foreach (var overrideEntity in overrides)
                    {
                        overridesDict[overrideEntity.name] = new
                        {
                            overrideEntity.check_type,
                            enabled = Convert.ToBoolean(overrideEntity.enabled),
                            flags = JsonSerializer.Deserialize<List<string>>(overrideEntity.flags) ?? new List<string>(),
                        };
                    }
                }

                if (admins.Any())
                {
                    foreach (var admin in admins.Where(a => a.expiry == 0 || a.expiry > _helper.GetTime()))
                    {
                        var groupNames = ResolveGroupNames(admin.groups, groups);
                        var commandOverrides = ResolveCommandOverrides(admin.overrides, overrides);

                        adminsDict[admin.player_name] = new
                        {
                            admin.identity,
                            flags = JsonSerializer.Deserialize<List<string>>(admin.flags) ?? new List<string>(),
                            admin.immunity,
                            groups = groupNames,
                            command_overrides = commandOverrides
                        };
                    }
                }
                string adminsJson = JsonSerializer.Serialize(adminsDict, new JsonSerializerOptions { WriteIndented = true });
                string groupsJson = JsonSerializer.Serialize(groupsDict, new JsonSerializerOptions { WriteIndented = true });
                string overridesJson = JsonSerializer.Serialize(overridesDict, new JsonSerializerOptions { WriteIndented = true });
                Server.NextWorldUpdate(() =>
                {
                    var pathFile = _helper.GetFilePath("configs/admin_groups.json");
                    pathFile = Path.GetFullPath(pathFile);
                    File.WriteAllTextAsync(pathFile, groupsJson);
                    Server.ExecuteCommand("css_groups_reload");

                    pathFile = _helper.GetFilePath("configs/admin_overrides.json");
                    pathFile = Path.GetFullPath(pathFile);
                    File.WriteAllTextAsync(pathFile, overridesJson);
                    Server.ExecuteCommand("css_overrides_reload");


                    pathFile = _helper.GetFilePath("configs/admins.json");
                    pathFile = Path.GetFullPath(pathFile);
                    File.WriteAllTextAsync(pathFile, adminsJson);
                    Server.ExecuteCommand("css_admins_reload");
                });

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing admins data: {ex.Message}");
            }
        });

    }

    //Q: Delete only per server ? or everything ?
    //A: hard to say. Let's keep deleting it globaly.
    public async Task DeleteExpiredAdminsAsync(int server_id)
    {
        string query = "DELETE FROM gcms_admins WHERE expiry <> 0 AND expiry < @time";
        var parameters = new Dictionary<string, object> { { "@time", _helper.GetTime() } };
        await Database.Instance.Update(query, parameters);
    }

    private Task<List<AdminEntity>> FetchAllAdmins(int server_id)
    {
        string query = "SELECT * FROM gcms_admins WHERE server_id = @server_id OR server_id = 0";
        var parameters = new Dictionary<string, object>
            {
                {"@server_id", server_id}
            };
        return Database.Instance.Query<AdminEntity>(query, parameters);
    }
    private static Task<List<AdminGroupEntity>> FetchAllGroups(int server_id)
    {
        string query = "SELECT * FROM gcms_admin_groups WHERE server_id = @server_id OR server_id = 0";
        var parameters = new Dictionary<string, object>
            {
                {"@server_id", server_id}
            };
        return Database.Instance.Query<AdminGroupEntity>(query, parameters);
    }
    private Task<List<AdminOverrideEntity>> FetchAllOverrides(int server_id)
    {
        string query = "SELECT * FROM gcms_admin_overrides WHERE server_id = @server_id OR server_id = 0";
        var parameters = new Dictionary<string, object>
            {
                {"@server_id", server_id}
            };
        return Database.Instance.Query<AdminOverrideEntity>(query, parameters);
    }

    private List<string> ResolveGroupNames(string groupsJson, List<AdminGroupEntity> allGroups)
    {
        if (groupsJson == "[]") return new List<string>();
        try
        {
            var groupIds = JsonSerializer.Deserialize<List<int>>(groupsJson);
            var ulongGroupIds = groupIds!.ConvertAll(id => (ulong)id);

            return allGroups.Where(g => g.id.HasValue && ulongGroupIds.Contains(g.id.Value)).Select(g => "#" + g.name).ToList()!;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while getting groups names {ex.Message}");
            return new List<string>();
        }
    }
    private Dictionary<string, bool> ResolveCommandOverrides(string overridesJson, List<AdminOverrideEntity> allOverrides)
    {
        if (string.IsNullOrEmpty(overridesJson) || overridesJson == "[]")
        {
            return new Dictionary<string, bool>();
        }

        try
        {
            var overrideIds = JsonSerializer.Deserialize<Dictionary<ulong, int>>(overridesJson);

            var resolvedOverrides = new Dictionary<string, bool>();

            // Assuming each override in the list is considered enabled

            //key = id
            //value = 1/0 enabled
            foreach (var entry in overrideIds!)
            {
                ulong id = entry.Key;

                var overrideData = allOverrides.FirstOrDefault(o => o.id == (ulong?)id);
                bool enabled = entry.Value != 0;

                if (overrideData != null)
                {
                    //in case global is disabled?
                    if (overrideData.enabled == 0)
                    {
                        enabled = false;
                    }
                    resolvedOverrides[overrideData.name!] = enabled;
                }
            }
            return resolvedOverrides;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while getting overrides {ex.Message}");
            return new Dictionary<string, bool>();
        }
    }

}
