namespace GameCMS.Models
{

    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Modules.Utils;
    using Microsoft.Extensions.Logging;

    public class PlayerModel
    {

        public readonly CCSPlayerController _controller;
        public ulong SteamID { get; private set; }
        public string PlayerName { get; private set; }
        public TimeData? TimeData { get; set; }


        public PlayerModel(CCSPlayerController playerController)
        {

            _controller = playerController;
            SteamID = playerController.SteamID;
            PlayerName = playerController.PlayerName;
        }
        public bool IsValid
        {
            get
            {
                return _controller?.IsValid == true 
                && _controller.PlayerPawn?.IsValid == true 
                && _controller.Connected == PlayerConnectedState.PlayerConnected;
            }
        }

        public bool IsPlayer
        {
            get
            {
                return !_controller.IsBot && !_controller.IsHLTV;
            }
        }

        public CsTeam GetPlayerTeam(){
            return _controller.Team;
        }

    }
}