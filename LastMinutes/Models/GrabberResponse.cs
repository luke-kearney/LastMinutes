namespace LastMinutes.Models
{
    public class GrabberResponse
    {

        public string trackName { get; set; } = string.Empty;
        public string artistName { get; set; } = string.Empty;
        public int durationMs { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public string searchTermTrack { get; set; } = string.Empty;
        public string searchTermArtist { get; set; } = string.Empty;

    }
}
