namespace LastMinutes.Models
{
    public class BadScrobbles
    {

        public int Id { get; set; }
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int PlayCount { get; set; }

    }
}
