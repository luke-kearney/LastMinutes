using System.ComponentModel.DataAnnotations.Schema;

namespace LastMinutes.Models.LMData
{
    [Table("LM_Tracks")]
    public class Tracks
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public int Runtime { get; set; }

        public DateTime Date_Added { get; set; }
        public DateTime Last_Used { get; set; }

    }
}
