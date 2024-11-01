using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LastMinutes.Models.LMData
{
    [Table("LM_Queue")]
    public class Queue
    {

        public int Id { get; set; }

        [MaxLength(1024)]
        public string Username { get; set; } = string.Empty;
        
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }

        public int Mode { get; set; }

        public bool SubmitToLeaderboard { get; set; } = false;

        [MaxLength(512)]
        public string Status { get; set; } = string.Empty;
        
        public int Retries { get; set; }
        public bool Failed { get; set; }

        public Queue(string username)
        {
            CreatedOn = DateTime.Now;
            UpdatedOn = DateTime.Now;
            Username = username;
            Retries = 0;
            Failed = false;
        }
    }
}
