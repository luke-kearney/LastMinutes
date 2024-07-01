using LastMinutes.Data;
using LastMinutes.Models;
using LastMinutes.Models.Deezer;
using LastMinutes.Models.LMData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LastMinutes.Services
{

    public interface IDeezerGrabber
    {

        public Task<Scrobble> GetScrobbleData(Scrobble ScrobbleIn);
        public Task<(string trackName, string artistName, int durationMs)> SearchForTrack(string trackName, string artistName);

    }


    public class DeezerGrabber : IDeezerGrabber
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DeezerGrabber> _logger;
        private readonly IConfiguration _config;
        private readonly ICacheManager _cache;

        private string DeezerApiUrl = string.Empty;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(3);

        public DeezerGrabber(
            IServiceProvider serviceProvider,
            ILogger<DeezerGrabber> logger,
            IConfiguration config,
            ICacheManager cache) 
        { 
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;
            _cache = cache;

            DeezerApiUrl = config.GetValue<string>("DeezerApiUrl") ?? "";
        }


        public async Task<(string trackName, string artistName, int durationMs)> SearchForTrack(string trackName, string artistName)
        {
            HttpClient httpClient = new HttpClient();

            string encodedTrackName = Uri.EscapeDataString(CleanSongName(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(trackName))));
            string encodedArtistName = Uri.EscapeDataString(artistName);
                

            //string searchUrl = $"{DeezerApiUrl}/search?q=artist:\"{encodedArtistName}\"track:\"{encodedTrackName}\"&limit=30";
            string searchUrl = $"{DeezerApiUrl}/search/track?q=\"{encodedTrackName} {encodedArtistName}\"&limit=30";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(searchUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<DeezerSearchResponse>(responseBody);

                    if (responseObject == null)
                    {
                        return (trackName, artistName, 0);
                    }

                    if (responseObject.Tracks == null)
                    {
                        return (trackName, artistName, 0);
                    }

                    if (responseObject.Tracks.Count != 0)
                    {
                        try
                        {
                            /*int StartIndex = 0;
                            var track = responseObject.Tracks[StartIndex];
                            int NameSimilarity = CompareStrings(track.Artist.Name.ToUpper(), artistName.ToUpper());

                            while (NameSimilarity < 37)
                            {
                                StartIndex++;
                                track = responseObject.Tracks[StartIndex];
                                NameSimilarity = CompareStrings(track.Artist.Name.ToUpper(), artistName.ToUpper());
                            }*/

                            List<DeezerTrack> tracksValidByArtistNamePerc = new();

                            foreach (var trackItem in responseObject.Tracks)
                            {
                                int NameSimilarityItem = CompareStrings(trackItem.Artist.Name.ToUpper(), artistName.ToUpper());
                                if (NameSimilarityItem >= 37)
                                {
                                    trackItem.SimilarityScore = CompareStrings(trackItem.Title.ToUpper(), trackName.ToUpper());
                                    trackItem.SimilarityScoreArtistName = NameSimilarityItem;
                                    tracksValidByArtistNamePerc.Add(trackItem);
                                }
                            }

                            var tracksValidByArtistNamePerc100 = tracksValidByArtistNamePerc.Where(x => x.SimilarityScoreArtistName == 100);
                            DeezerTrack? track = null;

                            if (tracksValidByArtistNamePerc100.Count() != 0)
                            {
                                track = tracksValidByArtistNamePerc100.OrderByDescending(x => x.SimilarityScore).First();
                            } else
                            {
                                track = tracksValidByArtistNamePerc.OrderByDescending(x => x.SimilarityScore).First();
                            }

                            
                            
                            /*if (track.Title.ToUpper().Contains("FRIDAY I'M IN"))
                            {
                                var filtered = tracksValidByArtistNamePerc.OrderByDescending(x => x.SimilarityScore).Take(100);
                                foreach (var filtereditem in filtered)
                                {
                                    _logger.LogCritical("T:{TrackName} A:{ArtistName} {SimilarityScore} {SimilarityScoreArtistName}", filtereditem.Title, filtereditem.Artist.Name, filtereditem.SimilarityScore, filtereditem.SimilarityScoreArtistName);
                                }
                            }
                            */
                            string trackNameResult = track.Title;
                            string artistNameResult = track.Artist.Name;
                            int durationMsResult = track.Duration * 1000;

                            Tracks CacheTrack = new Tracks()
                            {
                                Name = trackName,
                                Artist = artistName,
                                Runtime = durationMsResult,
                                AddedByResult_ArtistName = artistNameResult,
                                AddedByResult_Title = trackNameResult,
                                SimilarityScore_Title = track.SimilarityScore,
                                SimilarityScore_ArtistName = track.SimilarityScoreArtistName,
                                Source = "Deezer"
                            };

                            if (await _cache.AddTrackToCache(CacheTrack))
                                _logger.LogInformation("[Deezer] Track {TrackName} by {ArtistName} was added to the cache with a runtime of {Runtime}ms", trackName, artistName, durationMsResult);
                                

                            return (trackNameResult, artistNameResult, durationMsResult);
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
                    return ("UnknownResponseError", "ErrorException", 0);
                }
            } catch (Exception ex)
            {
                _logger.LogCritical(ex, "[Deezer] An unknown exception just occurred on SearchForTrack..");
                return ("UnknownResponseError", "ErrorException", 0);
            }
        }


        public async Task<Scrobble> GetScrobbleData(Scrobble ScrobbleIn)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var (t, a, ms) = await SearchForTrack(ScrobbleIn.TrackName, ScrobbleIn.ArtistName);
                int retries = 0;
                // If the rate limit is hit, run the track request that hit it one more time so that no track is excluded.
                while (t == "UnknownResponseError" && a == "ErrorException" && retries <= 5)
                {
                    _logger.LogError("[Deezer] 'UnknownResponseError' for track {TrackName} by {ArtistName}. Retrying {Retries}/5", ScrobbleIn.TrackName, ScrobbleIn.ArtistName, retries);
                    retries++;
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    (t, a, ms) = await SearchForTrack(ScrobbleIn.TrackName, ScrobbleIn.ArtistName);
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
