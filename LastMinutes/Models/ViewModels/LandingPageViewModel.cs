namespace LastMinutes.Models.ViewModels
{
    public class LandingPageViewModel
    {

        public bool ShowMessage { get; set; } = false;
        public string Message { get; set; } = string.Empty;

        public long TotalMinutes { get; set; }


        public string username { get; set; } = string.Empty;
        public string Mode { get; set; } = "3";
        public bool leaderboardSwitchInput { get; set; } = false;


    }
}
