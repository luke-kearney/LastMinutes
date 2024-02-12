namespace LastMinutes.Models.ViewModels
{
    public class ResultsViewModel
    {

        public string Username { get; set; } = string.Empty;
        public long TotalMs { get; set; }
        public string TotalMinutes { get; set; } = string.Empty;

        public List<Scrobble> TopScrobbles { get; set; } = new List<Scrobble>();
        public List<Scrobble> BadScrobbles { get; set; } = new List<Scrobble>();
        public string BadScrobbleText { get; set; } = "a few";

        public bool CanRefresh { get; set; } = false;
        public int Cooldown { get; set; }
        public string CooldownText { get; set; } = string.Empty;

    }

}
