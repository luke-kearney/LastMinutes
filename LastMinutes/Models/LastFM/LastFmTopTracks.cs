using Newtonsoft.Json;

namespace LastMinutes.Models.LastFM
{
    public class LastFmTopTracksResponse
    {
        [JsonProperty("toptracks")]
        public LastFmTopTracks TopTracks { get; set; }
    }

    public class LastFmTopTracks
    {
        [JsonProperty("track")]
        public List<LastFmTopTrack> Tracks { get; set; }

        [JsonProperty("@attr")]
        public LastFmTopTracksAttributes Attributes { get; set; }
    }

    public class LastFmTopTrack
    {
        [JsonProperty("mbid")]
        public string Mbid { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("artist")]
        public LastFmTopArtist Artist { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("duration")]
        public string Duration { get; set; }

        [JsonProperty("@attr")]
        public LastFmTopTrackAttributes Attributes { get; set; }

        [JsonProperty("playcount")]
        public string Playcount { get; set; }
    }

    public class LastFmTopArtist
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("mbid")]
        public string Mbid { get; set; }
    }

    public class LastFmTopTracksAttributes
    {
        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("totalPages")]
        public string TotalPages { get; set; }

        [JsonProperty("page")]
        public string Page { get; set; }

        [JsonProperty("perPage")]
        public string PerPage { get; set; }

        [JsonProperty("total")]
        public string Total { get; set; }
    }

    public class LastFmTopTrackAttributes
    {
        [JsonProperty("rank")]
        public string Rank { get; set; }
    }
}
