namespace LastMinutes.Models
{
    public class Scrobble
    {

        public int Id { get; set; }
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int Count { get; set; }
        public long Runtime { get; set; }
        public long TotalRuntime { get; set; }

    }
}
