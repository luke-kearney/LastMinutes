using System.ComponentModel.DataAnnotations.Schema;

namespace LastMinutes.Models.LMData
{
    [Table("LM_Leaderboard")]
    public class Leaderboard
    {

        public Guid Id { get; set; }

        public string Username { get; set; } = string.Empty;
        public long TotalMinutes { get; set; }

        public DateTime Created_On { get; set; }


    }
}
