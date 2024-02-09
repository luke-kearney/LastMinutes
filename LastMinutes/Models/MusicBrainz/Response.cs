namespace LastMinutes.Models.MusicBrainz
{
    public class Artist
    {
        public string id { get; set; }
        public string name { get; set; }
        public string sort_name { get; set; }
        public string disambiguation { get; set; }
    }

    public class ReleaseGroup
    {
        public string id { get; set; }
        public string type_id { get; set; }
        public string primary_type_id { get; set; }
        public string title { get; set; }
        public string primary_type { get; set; }
        public List<string> secondary_types { get; set; }
        public List<string> secondary_type_ids { get; set; }
    }

    public class Track
    {
        public string id { get; set; }
        public string number { get; set; }
        public string title { get; set; }
        public int length { get; set; }
    }

    public class Media
    {
        public int position { get; set; }
        public string format { get; set; }
        public List<Track> track { get; set; }
        public int track_count { get; set; }
        public int track_offset { get; set; }
    }

    public class Release
    {
        public string id { get; set; }
        public string status_id { get; set; }
        public int count { get; set; }
        public string title { get; set; }
        public string status { get; set; }
        public List<Artist> artist_credit { get; set; }
        public ReleaseGroup release_group { get; set; }
        public string date { get; set; }
        public string country { get; set; }
        public List<Media> media { get; set; }
        public int track_count { get; set; }
        public List<ReleaseEvent> release_events { get; set; }
    }

    public class ReleaseEvent
    {
        public string date { get; set; }
        public Area area { get; set; }
    }

    public class Area
    {
        public string id { get; set; }
        public string name { get; set; }
        public string sort_name { get; set; }
        public List<string> iso_3166_1_codes { get; set; }
    }

    public class Recording
    {
        public string id { get; set; }
        public int score { get; set; }
        public string title { get; set; }
        public int length { get; set; }
        public string disambiguation { get; set; }
        public object video { get; set; }
        public List<Artist> artist_credit { get; set; }
        public string first_release_date { get; set; }
        public List<Release> releases { get; set; }
    }

    public class MusicBrainsResponseRoot
    {
        public DateTime created { get; set; }
        public int count { get; set; }
        public int offset { get; set; }
        public List<Recording> recordings { get; set; }
        public bool success { get; set; } = true;
        public string errorMessage { get; set; } = string.Empty;
    }
}
