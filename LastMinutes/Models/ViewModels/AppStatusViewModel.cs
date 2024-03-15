namespace LastMinutes.Models.ViewModels
{
    public class AppStatusViewModel
    {

        public int QueueLength { get; set; }
        public int ResultsAmount { get; set; }

        public int TrackCache { get; set; }


        public long DeezerResponseTime { get; set; }
        public long SpotifyResponseTime { get; set; }
        public long LastFmResponseTime { get; set; }
    }
}
