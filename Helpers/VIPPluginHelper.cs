using System.Collections;
using System.Collections.Generic;
using System.Net;
using CounterStrikeSharp.API.Core.Capabilities;
using Newtonsoft.Json;
using VipCoreApi;
using static VipCoreApi.IVipCoreApi;

namespace GameCMS
{


    public class VIPPluginHelper
    {

        private HttpServerSerivce httpServerSerivce;
        private Helper _helper;


        public VIPPluginHelper(HttpServerSerivce httpServerSerivce)
        {
            this.httpServerSerivce = httpServerSerivce;
            this._helper = httpServerSerivce.getHelper();
        }


        public async Task GetVIPGroups(HttpListenerContext context)
        {

            // File path for the VIP-Plugin.json
            string filePath = _helper.GetFilePath("configs/plugins/VIP-Plugin/VIP-Plugin.json");

            // Check if the file exists
            if (!File.Exists(filePath))
            {
                await httpServerSerivce.SendJsonResponse(context, new { message = "VIP-Plugin does not exist in your server." }, 400);
                return;
            }

            try
            {
                string jsonContent = await File.ReadAllTextAsync(filePath);

                var jsonData = JsonConvert.DeserializeObject<dynamic>(jsonContent);

                if (jsonData?.VIPGroups == null || jsonData!.VIPGroups.Count == 0)
                {
                    await httpServerSerivce.SendJsonResponse(context, new { message = "VIPGroups array is empty or not found." }, 400);
                    return;
                }

                var vipGroups = new List<dynamic>();

                foreach (var group in jsonData!.VIPGroups)
                {
                    var permissions = group.Permissions != null ? (string)group.Permissions.ToString() : string.Empty;
                    var name = group.Name != null ? (string)group.Name.ToString() : string.Empty;
                    var uniqueId = group.UniqueId != null ? (string)group.UniqueId.ToString() : string.Empty;

                    // Creating a dynamic object and adding it to the list
                    var vipGroup = new
                    {
                        permissions = permissions,
                        name = name,
                        id = uniqueId
                    };

                    vipGroups.Add(vipGroup);
                }
                await httpServerSerivce.SendJsonResponse(context, new { groups = vipGroups }, 200);
            }
            catch (Exception ex)
            {
                await httpServerSerivce.SendJsonResponse(context, new { message = "Error reading or parsing VIP-Plugin.json", error = ex.Message }, 500);
            }
        }
    }

}