namespace GameCMS
{
    using System.Text.Json.Serialization;

    public class K4SystemRankEntity
    {
        [JsonPropertyName("server_id")]
        public int server_id { get; set; }

        [JsonPropertyName("Name")]
        public string name { get; set; } = string.Empty;

        [JsonPropertyName("Tag")]
        public string tag { get; set; } = string.Empty;

        [JsonPropertyName("Image")]
        public string? image { get; set; } = string.Empty;

        [JsonPropertyName("Color")]
        public string color { get; set; } = string.Empty;

        [JsonPropertyName("Point")]
        public long points { get; set; } = 0;

    }

}