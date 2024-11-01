namespace LastMinutes.Models.LastFM
{
    public class FetchLastFmUser
    {
        public User? User { get; set; }
    }
    
    
    public class GetLastFmUserResponse
    {
        public User? User { get; set; }
        public bool Success { get; set; }
        
        public int ResponseCode { get; set; }
        public string ErrorMessage { get; set; }

        public GetLastFmUserResponse(bool success, User? user = null, string errorMessage = "", int responseCode = 200)
        {
            Success = success;
            User = user;
            ErrorMessage = errorMessage;
            ResponseCode = responseCode;
        }
    }

    public class User
    {
        public string Name { get; set; }
        public string Age { get; set; }
        public int Subscriber { get; set; }
        public string RealName { get; set; }
        public int Bootstrap { get; set; }
        public int Playcount { get; set; }
        public int ArtistCount { get; set; }
        public int Playlists { get; set; }
        public int TrackCount { get; set; }
        public int AlbumCount { get; set; }
        public List<LastFMUserImage> Image { get; set; }
        public Registered Registered { get; set; }
        public string Country { get; set; }
        public string Gender { get; set; }
        public string Url { get; set; }
        public string Type { get; set; }
    }

    public class LastFMUserImage
    {
        public string Size { get; set; }
        public string Text { get; set; }
    }

    public class Registered
    {
        public string Unixtime { get; set; }
        public long Text { get; set; }

        // Add a property to convert Unixtime to DateTime
        public DateTime RegisteredDateTime => DateTimeOffset.FromUnixTimeSeconds(Text).UtcDateTime;
    }
}
