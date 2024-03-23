
using System.Net;
using System.Text.Json;
using Main;
using Services;

namespace Helpers;
public class VIPCoreHelper
{

    private HttpServerSerivce httpServer { get; }
    private readonly Helper _helper;

    public VIPCoreHelper(HttpServerSerivce gameCMSHttpServer)
    {
        httpServer = gameCMSHttpServer;
        _helper = httpServer.getHelper();
    }

    public async Task GetVipCoreServerId(HttpListenerContext context)
    {
        string vipConfigPath = _helper.GetFilePath("configs/plugins/VIPCore/vip.json");
        if (File.Exists(vipConfigPath))
        {
            string jsonContent = File.ReadAllText(vipConfigPath);
            var configObject = JsonSerializer.Deserialize<VipCoreConfig>(jsonContent);

            if (configObject != null)
            {
                await httpServer.SendJsonResponse(context, new { server_id = configObject.ServerId }, 200);
            }
            else
            {
                await httpServer.SendJsonResponse(context, new { message = "Unable to parse VIP-Core config file." }, 500);
            }
        }
        else
        {
            await httpServer.SendJsonResponse(context, new { message = "VIP-Core config file not found at " + vipConfigPath }, 404);
        }
    }

    public async Task GetVipCoreFolders(HttpListenerContext context)
    {
        string vipFolderPath = _helper.GetFilePath("plugins/ModularityPlugin/plugins");
      
        if (Directory.Exists(vipFolderPath))
        {

            var directoryNames = Directory.GetDirectories(vipFolderPath)
                .Select(Path.GetFileName)
                .Where(name => name?.StartsWith("VIP_") == true)
                .Select(name => name?[4..])
                .Where(name => name != null)
                .ToList();

            await httpServer.SendJsonResponse(context, new { data = directoryNames }, 200);
        }
        else
        {
            await httpServer.SendJsonResponse(context, new { message = "VIP-Core Modules folder not found at " + vipFolderPath }, 404);
        }
    }

    public class VipCoreConfig
    {
        public int ServerId { get; set; }
    }
}