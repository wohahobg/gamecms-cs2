namespace GameCMS
{
    public class TimeData
    {
        public Dictionary<string, DateTime> Times { get; set; } = new Dictionary<string, DateTime>();
        public Dictionary<string, int> TimeFields { get; set; } = new Dictionary<string, int>();
    }
}