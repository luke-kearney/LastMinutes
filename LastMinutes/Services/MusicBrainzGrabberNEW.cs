using LastMinutes.Data;
using LastMinutes.Models;
using LastMinutes.Models.Deezer;
using LastMinutes.Models.LMData;
using LastMinutes.Models.MusicBrainz;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LastMinutes.Services
{

    public interface IMusicBrainz
    {

        public Task<Scrobble> GetData(Scrobble ScrobbleIn);
        public Task<(string trackName, string artistName, int durationMs)> GetScrobbleData(string trackName, string artistName);

    }


    public class MusicBrainz : IMusicBrainz
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;


        private string MusicBrainzApiUrl = string.Empty;
        private string MusicBrainzUserAgent = string.Empty;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);

        public MusicBrainz(
            IServiceProvider serviceProvider,
            IConfiguration config) 
        { 
            _serviceProvider = serviceProvider;
            _config = config;

            MusicBrainzApiUrl = config.GetValue<string>("MusicBrainzApiUrl");
            MusicBrainzUserAgent = config.GetValue<string>("MusicBrainzUserAgent");
        }


        public async Task<(string trackName, string artistName, int durationMs)> GetScrobbleData(string trackName, string artistName)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                HttpClient httpClient = new HttpClient();

                string encodedTrackName = Uri.EscapeDataString(CleanSongName(trackName));
                string encodedArtistName = Uri.EscapeDataString(artistName);
                int responseMinScore = 100;
                int responseLimit = 1;

                string searchUrl = $"{MusicBrainzApiUrl}/recording?query=artist:\"{encodedArtistName}\" AND recording:\"{encodedTrackName}\"&min-score={responseMinScore}&limit={responseLimit}&fmt=json";

                HttpResponseMessage response = await httpClient.GetAsync(searchUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<MusicBrainsResponseRoot>(responseBody);

                    if (responseObject == null)
                    {
                        return (trackName, artistName, 0);
                    }

                    if (responseObject.recordings == null)
                    {
                        return (trackName, artistName, 0);
                    }

                    if (responseObject.recordings.Count != 0)
                    {
                        try
                        {
                            /*int StartIndex = 0;
                            var track = responseObject.recordings[StartIndex];
                            int NameSimilarity = CompareStrings(track.artist_credit.Name.ToUpper(), artistName.ToUpper());

                            while (NameSimilarity < 37)
                            {
                                StartIndex++;
                                track = responseObject.recordings[StartIndex];
                                NameSimilarity = CompareStrings(track.Artist.Name.ToUpper(), artistName.ToUpper());
                            }

                            string trackNameResult = track.Title;
                            string artistNameResult = track.Artist.Name;
                            int durationMsResult = track.Duration * 1000;

                            Tracks CacheTrack = new Tracks()
                            {
                                Name = trackName,
                                Artist = artistName,
                                Runtime = durationMsResult,
                                Date_Added = DateTime.Now,
                                Last_Used = DateTime.Now
                            };

                            _lmdata.Tracks.Add(CacheTrack);
                            if (await _lmdata.SaveChangesAsync() > 0)
                            {
                                Console.WriteLine("");
                                Console.WriteLine($"[Deezer] Search Term: '{trackName}' by '{artistName}'");
                                Console.WriteLine($"[Cache]  Track Added: '{track.Title}' by '{track.Artist.Name}', runtime '{durationMsResult}'");
                            }
                            */
                            return ("", "", 2);
                        }
                        catch
                        {
                            return (trackName, artistName, 0);
                        }

                    }
                    else
                    {
                        return (trackName, artistName, 0);
                    }
                }
                else
                {

                    Console.WriteLine($"[Deezer] Error occurred while asking Deezer about '{trackName}' by {artistName}..");
                    Console.WriteLine($"[Deezer] Error is {response.StatusCode}");
                    return ("UnknownResponseError", "ErrorException", 0);
                }



            }
            

        }


        public async Task<Scrobble> GetData(Scrobble ScrobbleIn)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var (t, a, ms) = await GetScrobbleData(ScrobbleIn.TrackName, ScrobbleIn.ArtistName);

                // If the rate limit is hit, run the track request that hit it one more time so that no track is excluded.
                while (t == "UnknownResponseError" && a == "ErrorException")
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    (t, a, ms) = await GetScrobbleData(ScrobbleIn.TrackName, ScrobbleIn.ArtistName);
                }

                if (ms != 0)
                {
                   // Console.WriteLine($"[Deezer] Found a new track '{t}' by '{a}' with an ms runtime of {ms.ToString()}");
                }
                

                ScrobbleIn.Runtime = ms;
                ScrobbleIn.TotalRuntime = ms * ScrobbleIn.Count;

                return ScrobbleIn;

            }
            finally
            {
                semaphoreSlim.Release();    
            }
        }



        private static string CleanSongName(string songName)
        {
            // Remove common substrings
            string cleanedName = Regex.Replace(songName, @"\s*\b(Feat\.?|ft\.?|Feat)\s+.*", "", RegexOptions.IgnoreCase);

            // Remove parentheses and their content
            cleanedName = Regex.Replace(cleanedName, @"\s*\([^()]*\)", "");

            // Remove "ft." and ","
            cleanedName = cleanedName.Replace("ft.", "").Replace(",", "");

            // Remove leading and trailing spaces
            cleanedName = cleanedName.Trim();

            // Replace remaining spaces with a single space
            cleanedName = Regex.Replace(cleanedName, @"\s+", " ");

            return cleanedName;
        }


        public static int CompareStrings(string str1, string str2)
        {
            int[,] distance = new int[str1.Length + 1, str2.Length + 1];

            for (int i = 0; i <= str1.Length; i++)
            {
                for (int j = 0; j <= str2.Length; j++)
                {
                    if (i == 0)
                    {
                        distance[i, j] = j;
                    }
                    else if (j == 0)
                    {
                        distance[i, j] = i;
                    }
                    else
                    {
                        distance[i, j] = Math.Min(Math.Min(
                            distance[i - 1, j] + 1,
                            distance[i, j - 1] + 1),
                            distance[i - 1, j - 1] + (str1[i - 1] == str2[j - 1] ? 0 : 1));
                    }
                }
            }

            int maxLength = Math.Max(str1.Length, str2.Length);
            int similarity = (int)((1.0 - (double)distance[str1.Length, str2.Length] / maxLength) * 100);

            // Check if the strings contain a common word
            string[] words1 = str1.Split(' ');
            string[] words2 = str2.Split(' ');
            bool containsCommonWord = false;
            foreach (string word1 in words1)
            {
                foreach (string word2 in words2)
                {
                    if (word1.Equals(word2, StringComparison.OrdinalIgnoreCase))
                    {
                        containsCommonWord = true;
                        break;
                    }
                }
                if (containsCommonWord)
                {
                    break;
                }
            }

            // Increase similarity if common word found
            if (containsCommonWord)
            {
                similarity = Math.Min(similarity + 15, 100); // Adjust the value as needed
            }

            return similarity;
        }



    }
}
