using LastMinutes.Data;
using LastMinutes.Models.LMData;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Linq;

namespace LastMinutes.Services
{

    public interface ISpotifyGrabber
    {

        public Task<(string trackName, string artistName, int durationMs)> SearchForTrack(string trackName, string artistName, string authToken);

        public Task<Dictionary<(string trackName, string artistName), int>> GetTrackRuntime(string trackName, string artistName, int count, string authToken);

        public Task<string> GetAccessToken();

        public string ConvertMsToMinutes(int durationMs);

    }


    public class SpotifyGrabber : ISpotifyGrabber
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        private string SpotifyApiUrl = string.Empty;
        private string SpotifyAccUrl = string.Empty;
        private string SpotifyClientId = string.Empty;
        private string SpotifyClientSecret = string.Empty;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(4);

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


        public async Task<(string trackName, string artistName, int durationMs)> SearchForTrack(string trackName, string artistName, string authToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();    

                Tracks CachedTrack = _lmdata.Tracks.Where(x => x.Name == trackName && x.Artist == artistName).FirstOrDefault();

                if (CachedTrack != null)
                {
                    CachedTrack.Last_Used = DateTime.Now;
                    _lmdata.Tracks.Update(CachedTrack);
                    await _lmdata.SaveChangesAsync();

                    return (CachedTrack.Name, CachedTrack.Artist, CachedTrack.Runtime);

                } else
                {
                    HttpClient httpClient = new HttpClient();

                    string encodedTrackName = Uri.EscapeDataString(trackName);
                    string encodedArtistName = Uri.EscapeDataString(artistName);


                    string searchUrl = $"{SpotifyApiUrl}/search?q={encodedTrackName}+artist:{encodedArtistName}&type=track&limit=1";

                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

                    HttpResponseMessage response = await httpClient.GetAsync(searchUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var responseObject = JsonConvert.DeserializeObject<SpotifySearchResponse>(responseBody);

                        if (responseObject == null)
                        {
                            return (trackName, artistName, 0);
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
                                Runtime = durationMsResult
                            };

                            _lmdata.Tracks.Add(CacheTrack);
                            await _lmdata.SaveChangesAsync();

                            return (trackNameResult, artistNameResult, durationMsResult);
                        }
                        else
                        {
                            return (trackName, artistName, 0);
                        }
                    }
                    else
                    {
                        return ("TrackNotFound", "ErrorException", 0);
                    }
                }

            }

            

        }


        public async Task<Dictionary<(string, string), int>> GetTrackRuntime(string trackName, string artistName, int count, string authToken)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var (t, a, ms) = await SearchForTrack(trackName, artistName, authToken);
                var runtime = new Dictionary<(string trackName, string artistName), int>();

                Console.WriteLine($"[Spotify] Found track '{t}' by '{a}' with an ms runtime of {ms.ToString()}");

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


        public string ConvertMsToMinutes(int durationMs)
        {
            // Convert milliseconds to minutes
            int totalMinutes = durationMs / (1000 * 60);

            // Return the result as a string
            return totalMinutes.ToString();
        }


    }
}
