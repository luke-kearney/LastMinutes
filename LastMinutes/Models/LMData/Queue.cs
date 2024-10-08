﻿using System.ComponentModel.DataAnnotations.Schema;

namespace LastMinutes.Models.LMData
{
    [Table("LM_Queue")]
    public class Queue
    {

        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;
        
        public DateTime Created_On { get; set; }
        public DateTime Updated_On { get; set; }

        public int Mode { get; set; }

        public bool submitToLeaderboard { get; set; } = false;

        public string Status { get; set; } = string.Empty;

        public Queue()
        {
            Created_On = DateTime.Now;
            Updated_On = DateTime.Now;
        }
    }
}
