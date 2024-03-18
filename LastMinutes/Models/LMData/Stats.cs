using System.ComponentModel.DataAnnotations.Schema;

namespace LastMinutes.Models.LMData
{
    [Table("LM_Stats")]
    public class Stats
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;

    }
}
