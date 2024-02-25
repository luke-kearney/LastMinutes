using System;
using System.Collections.Generic;

namespace LastMinutes.Models.LastFM
{


    public class LastFmTrackArtist
    {
        public string name { get; set; }
        public string mbid { get; set; }
        public string url { get; set; }
    }


    public class LastFmTrackAlbum
    {
        public string artist { get; set; }
        public string title { get; set; }
        public string mbid { get; set; }
        public string url { get; set; }
    }

    public class LastFmTrackData
    {
        public string name { get; set; }
        public string mbid { get; set; }
        public string url { get; set; }
        public string duration { get; set; } = "0";
        public string listeners { get; set; }
        public string playcount { get; set; }
        public LastFmTrackArtist artist { get; set; }
        public LastFmTrackAlbum album { get; set; }
    }

    public class LastFmTrack
    {
        public LastFmTrackData track { get; set; }
    }

}
