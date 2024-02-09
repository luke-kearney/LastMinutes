using System.Collections.Generic;
using Newtonsoft.Json;

namespace LastMinutes.Models.Deezer
{
    public class DeezerSearchResponse
    {
        [JsonProperty("data")]
        public List<DeezerTrack> Tracks { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }
    }

    public class DeezerTrack
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("readable")]
        public bool Readable { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("title_short")]
        public string TitleShort { get; set; }

        [JsonProperty("title_version")]
        public string TitleVersion { get; set; }

        [JsonProperty("link")]
        public string Link { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("explicit_lyrics")]
        public bool ExplicitLyrics { get; set; }

        [JsonProperty("explicit_content_lyrics")]
        public int ExplicitContentLyrics { get; set; }

        [JsonProperty("explicit_content_cover")]
        public int ExplicitContentCover { get; set; }

        [JsonProperty("preview")]
        public string Preview { get; set; }

        [JsonProperty("md5_image")]
        public string Md5Image { get; set; }

        [JsonProperty("artist")]
        public DeezerArtist Artist { get; set; }

        [JsonProperty("album")]
        public DeezerAlbum Album { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class DeezerArtist
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("link")]
        public string Link { get; set; }

        [JsonProperty("picture")]
        public string Picture { get; set; }

        [JsonProperty("picture_small")]
        public string PictureSmall { get; set; }

        [JsonProperty("picture_medium")]
        public string PictureMedium { get; set; }

        [JsonProperty("picture_big")]
        public string PictureBig { get; set; }

        [JsonProperty("picture_xl")]
        public string PictureXl { get; set; }

        [JsonProperty("tracklist")]
        public string Tracklist { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class DeezerAlbum
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("cover")]
        public string Cover { get; set; }

        [JsonProperty("cover_small")]
        public string CoverSmall { get; set; }

        [JsonProperty("cover_medium")]
        public string CoverMedium { get; set; }

        [JsonProperty("cover_big")]
        public string CoverBig { get; set; }

        [JsonProperty("cover_xl")]
        public string CoverXl { get; set; }

        [JsonProperty("md5_image")]
        public string Md5Image { get; set; }

        [JsonProperty("tracklist")]
        public string Tracklist { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

}


