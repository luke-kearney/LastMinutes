using LastMinutes.Models.LMData;

namespace LastMinutes.Models.ViewModels
{
    public class LeaderboardViewModel
    {

        public List<Leaderboard> leaderboardEntries { get; set; } = new List<Leaderboard>();

        public long TotalMinutes { get; set; }


    }

}
