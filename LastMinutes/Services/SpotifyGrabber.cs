using LastMinutes.Data;
using LastMinutes.Models.LMData;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Linq;

namespace LastMinutes.Services
{

    public interface ISpotifyGrabber
    {

        public Task<(string trackName, string artistName, int durationMs, bool Cached)> SearchForTrack(string trackName, string artistName, string authToken, List<Tracks> AllCached);

        public Task<Dictionary<(string trackName, string artistName), int>> GetTrackRuntime(string trackName, string artistName, int count, string authToken, List<Tracks> AllCached);

        public Task<string> GetAccessToken();

        public string ConvertMsToMinutes(long durationMs);

    }


    public class SpotifyGrabber : ISpotifyGrabber
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        private string SpotifyApiUrl = string.Empty;
        private string SpotifyAccUrl = string.Empty;
        private string SpotifyClientId = string.Empty;
        private string SpotifyClientSecret = string.Empty;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(2);

        public SpotifyGrabber(
            IServiceProvider serviceProvider,
            IConfiguration config) 
        { 
            _serviceProvider = serviceProvider;
            _config = config;   

            SpotifyApiUrl = config.GetValue<string>("SpotifyApiUrl");
            SpotifyAccUrl = config.GetValue<string>("SpotifyAccUrl");
            SpotifyClientId = config.GetValue<string>("SpotifyClientId");
            SpotifyClientSecret = config.GetValue<string>("SpotifyClientSecret");
        }


        public async Task<(string trackName, string artistName, int durationMs, bool Cached)> SearchForTrack(string trackName, string artistName, string authToken, List<Tracks> AllCached)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();    

                if (AllCached == null)
                {
                    AllCached = new List<Tracks>();
                }

                Tracks CachedTrack = AllCached.Where(x => x.Name == trackName && x.Artist == artistName).FirstOrDefault();

                if (CachedTrack != null && CachedTrack.Artist != "ErrorException")
                {
                    CachedTrack.Last_Used = DateTime.Now;
                    _lmdata.Tracks.Update(CachedTrack);
                    await _lmdata.SaveChangesAsync();

                    return (CachedTrack.Name, CachedTrack.Artist, CachedTrack.Runtime, true);

                } else
                {
                    HttpClient httpClient = new HttpClient();

                    string encodedTrackName = Uri.EscapeDataString(trackName);
                    string encodedArtistName = Uri.EscapeDataString(artistName);


                    string searchUrl = $"{SpotifyApiUrl}/search?q={encodedTrackName}+artist:{encodedArtistName}&type=track&limit=4";

                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

                    HttpResponseMessage response = await httpClient.GetAsync(searchUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var responseObject = JsonConvert.DeserializeObject<SpotifySearchResponse>(responseBody);

                        if (responseObject == null)
                        {
                            return (trackName, artistName, 0, false);
                        }

                        if (responseObject.Tracks.Items.Count() != 0)
                        {
                            var track = responseObject.Tracks.Items[0];
                            string trackNameResult = track.Name;
                            string artistNameResult = track.Artists[0].Name;
                            int durationMsResult = track.duration_ms;

                            Tracks CacheTrack = new Tracks()
                            {
                                Name = trackNameResult,
                                Artist = artistNameResult,
                                Runtime = durationMsResult,
                                Date_Added = DateTime.Now,
                                Last_Used = DateTime.Now
                            };

                            _lmdata.Tracks.Add(CacheTrack);
                            await _lmdata.SaveChangesAsync();

                            return (trackNameResult, artistNameResult, durationMsResult, false);
                        }
                        else
                        {
                            return (trackName, artistName, 0, false);
                        }
                    }
                    else
                    {
                        
                        Console.WriteLine($"[Spotify] Error occurred while asking Spotify about '{trackName}' by {artistName}..");
                        Console.WriteLine($"[Spotify] Error is {response.StatusCode}" );

                        // if the response is TooManyRequests, retrieve the cool-down time and wait for that amount before returning an empty/exception variable.
                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            string retryAfterHeader = response.Headers.GetValues("Retry-After").FirstOrDefault() ?? "30";
                            Console.WriteLine($"[Spotify] Pausing for {retryAfterHeader} seconds.");
                            await Task.Delay(TimeSpan.FromSeconds(Int32.Parse(retryAfterHeader)));
                            return ("SpotifyRateLimitHit", "ErrorException", 0, false);
                        }

                        return ("UnknownResponseError", "ErrorException", 0, false);
                    }
                }

            }

            

        }


        public async Task<Dictionary<(string, string), int>> GetTrackRuntime(string trackName, string artistName, int count, string authToken, List<Tracks> AllCached)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var (t, a, ms, ch) = await SearchForTrack(trackName, artistName, authToken, AllCached);

                // If the rate limit is hit, run the track request that hit it one more time so that no track is excluded.
                while (t == "SpotifyRateLimitHit" && a == "ErrorException")
                {
                    (t, a, ms, ch) = await SearchForTrack(trackName, artistName, authToken, AllCached);
                }

                var runtime = new Dictionary<(string trackName, string artistName), int>();

                // Check if the track data was retrieved from Spotify or from the DB cache.
                if (ch)
                {
                    Console.WriteLine($"[Spotify] Found a cached track '{t}' by '{a}' with an ms runtime of {ms.ToString()}");
                } else
                {
                    Console.WriteLine($"[Spotify] Found a new track '{t}' by '{a}' with an ms runtime of {ms.ToString()}");
                }

                runtime.Add((t, a), ms * count);

                return runtime;

            }
            finally
            {
                semaphoreSlim.Release();    
            }
        }

        public async Task<string> GetAccessToken()
        {
            try
            {
                var base64AuthHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SpotifyClientId}:{SpotifyClientSecret}"));

                using var httpClient = new HttpClient();

                // Construct request content
                var requestBodyContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                // Add Authorization header
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64AuthHeader);

                // Send HTTP POST request to Spotify Accounts Service
                HttpResponseMessage response = await httpClient.PostAsync(SpotifyAccUrl, requestBodyContent);

                // Check if the request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Deserialize JSON response
                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic tokenResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent);

                    return tokenResponse.access_token;
                }
                else
                {
                    // Handle error response from Spotify Accounts Service
                    return "tokenfailure";
                }
            }
            catch (Exception ex)
            {
                // Handle exception
                return "tokenfailure";
            }
        }
        private class SpotifySearchResponse
        {
            public TracksData Tracks { get; set; }

            public class TracksData
            {
                public TrackItem[] Items { get; set; }
            }

            public class TrackItem
            {
                public string Name { get; set; }
                public ArtistItem[] Artists { get; set; }
                public int duration_ms { get; set; }
            }

            public class ArtistItem
            {
                public string Name { get; set; }
            }
        }


        public string ConvertMsToMinutes(long durationMs)
        {
            // Convert milliseconds to minutes
            long totalMinutes = durationMs / (1000 * 60);

            // Return the result as a string
            return totalMinutes.ToString();
        }


    }
}
