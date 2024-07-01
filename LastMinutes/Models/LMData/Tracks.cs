using System.ComponentModel.DataAnnotations.Schema;

namespace LastMinutes.Models.LMData
{
    [Table("LM_Tracks")]
    public class Tracks
    {

        /* 
         
        In this model, Name and Artist are the search terms that were used to find the track.
        AddedByResult_* is the values that were returned and added to cache for the search terms. 

        I added these two additional fields to keep track of what was searched and what was selected from the return.
        DeezerGrabber should be pretty accurate in finding the correct track, but I'm not sure about Spotify.

        Keeping track of this information should allow us to be able to see how accurate each source is.
          
        */


        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public int Runtime { get; set; }

        public string Source { get; set; } = string.Empty;

        public DateTime Date_Added { get; set; }
        public DateTime Last_Used { get; set; }

        public string AddedByResult_Title { get; set; } = "";
        public string AddedByResult_ArtistName { get; set; } = "";

        public int SimilarityScore_Title { get; set; } = 0;
        public int SimilarityScore_ArtistName { get; set; } = 0;

    }
}
