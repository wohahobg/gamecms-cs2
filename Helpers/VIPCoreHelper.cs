using System.Collections;
using System.Collections.Generic;
using System.Net;
using CounterStrikeSharp.API.Core.Capabilities;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace GameCMS
{


    public class VIPCoreHelper
    {



        private IVipCoreApi? _api;
        private PluginCapability<IVipCoreApi> PluginCapability { get; } = new("vipcore:core");

        private HttpServerSerivce httpServerSerivce;

        private VipCoreConfig Config = null!;

        public VIPCoreHelper(HttpServerSerivce httpServerSerivce)
        {
            this.httpServerSerivce = httpServerSerivce;
        }


        public async Task GetVipCoreServer(HttpListenerContext context)
        {
            _api = PluginCapability.Get();
            if (_api == null)
            {
                await httpServerSerivce.SendJsonResponse(context, new { message = "VIP Core does not exist on this server?" }, 400);
                return;
            }
            Config = _api.LoadConfig<VipCoreConfig>("vip_core", _api.CoreConfigDirectory);
            string path = httpServerSerivce.getHelper().GetFilePath("plugins");

            // Get all directories with the prefix "VIP_"
            var vipDirectories = Directory.GetDirectories(path, "VIP_*", SearchOption.TopDirectoryOnly);

            // Remove "VIP_" prefix and store the remaining names in a list
            List<string> modules = vipDirectories
                                    .Select(dir => Path.GetFileName(dir))
                                    .Where(name => name.StartsWith("VIP_"))
                                    .Select(name => name.Substring(4))
                                    .ToList();


            int serverId = Config.ServerId;
            string[] groups = _api.GetVipGroups();
            await httpServerSerivce.SendJsonResponse(context, new { id = serverId, groups, modules }, 200);
            return;
        }
    }

    public class VipCoreConfig
    {
        public int ServerId { get; set; } = 0;
    }
}