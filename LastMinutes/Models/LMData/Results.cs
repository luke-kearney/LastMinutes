using System.ComponentModel.DataAnnotations.Schema;

namespace LastMinutes.Models.LMData
{
    [Table("LM_Results")]
    public class Results 
    {

        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public long TotalPlaytime { get; set; }

        public string AllScrobbles { get; set; } = string.Empty;

        public DateTime Created_On { get; set; }


        public Results()
        {
            Created_On = DateTime.Now;
        }

    }
}
