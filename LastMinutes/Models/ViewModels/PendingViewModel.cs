﻿namespace LastMinutes.Models.ViewModels
{
    public class PendingViewModel
    {

        public string Username { get; set; } = "UsernameInvalid";
        public int Eta { get; set; }
        public string EtaWords { get; set; } = "Unknown";
        public string ServerStatus { get; set; } = "Unknown";

    }
}
