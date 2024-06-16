namespace GameCMS
{

    public class PlayerUpdateDto
    {
        public ulong SteamId { get; set; }
        public string Username { get; set; }
        public int ServerId { get; set; }
        public DateTime TodayDate { get; set; }
        public long TodayUnixTimestamp { get; set; }
        public int? Ct { get; set; }
        public int? T { get; set; }
        public int? Spec { get; set; }
    }
}
