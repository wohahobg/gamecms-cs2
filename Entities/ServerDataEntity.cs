
namespace GameCMS
{

    public class ServerDataEntity(
               IEnumerable<PlayerInfoEntityServerData> players,
               string map,
               string ip,
               int maxPlayers,
               int onlinePlayers,
               int roundWinCt,
               int roundWinT,
               float MaxRoundTime,
               int MaxRounds,
               long matchStartTime,
               bool isWarmupRound)
    {
        public IEnumerable<PlayerInfoEntityServerData> players { get; set; } = players;
        public string map { get; set; } = map;
        public string ip { get; set; } = ip;
        public int max_players { get; set; } = maxPlayers;
        public int online_players { get; set; } = onlinePlayers;
        public int round_win_ct { get; set; } = roundWinCt;
        public int round_win_t { get; set; } = roundWinT;
        public float match_max_time { get; set; } = MaxRoundTime;
        public int match_max_rounds { get; set; } = MaxRounds;
        public long match_start_time { get; set; } = matchStartTime;
        public bool is_warm_up_round { get; set; } = isWarmupRound;
    }

}