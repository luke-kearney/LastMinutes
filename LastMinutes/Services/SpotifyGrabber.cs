using LastMinutes.Data;
using LastMinutes.Models;
using LastMinutes.Models.LMData;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Linq;

namespace LastMinutes.Services
{

    public interface ISpotifyGrabber
    {

        public Task<(string trackName, string artistName, int durationMs)> SearchForTrack(string trackName, string artistName, string authToken);

        public Task<Scrobble> GetScrobbleData(Scrobble ScrobbleIn, string authToken);

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


        public async Task<(string trackName, string artistName, int durationMs)> SearchForTrack(string trackName, string artistName, string authToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

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
                        return (trackName, artistName, 0);
                    }

                    if (responseObject.Tracks.Items.Count() != 0)
                    {
                        try
                        {
                            var track = responseObject.Tracks.Items[0];
                            string trackNameResult = track.Name;
                            string artistNameResult = track.Artists[0].Name;
                            int durationMsResult = track.duration_ms;

                            Tracks CacheTrack = new Tracks()
                            {
                                Name = trackName,
                                Artist = artistName,
                                Runtime = durationMsResult,
                                Date_Added = DateTime.Now,
                                Last_Used = DateTime.Now,
                                Source = "Spotify"
                            };

                            _lmdata.Tracks.Add(CacheTrack);
                            if (await _lmdata.SaveChangesAsync() > 0)
                            {
                                Console.WriteLine($"[Cache] New item added: '{trackNameResult}' by '{artistNameResult}', runtime '{durationMsResult}'");
                                Console.WriteLine($"[Cache] Search Term Wa: '{trackName}' by '{artistName}', runtime '{durationMsResult}'");
                                Console.WriteLine("");
                            }

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

                    Console.WriteLine($"[Spotify] Error occurred while asking Spotify about '{trackName}' by {artistName}..");
                    Console.WriteLine($"[Spotify] Error is {response.StatusCode}");

                    // if the response is TooManyRequests, retrieve the cool-down time and wait for that amount before returning an empty/exception variable.
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        string retryAfterHeader = response.Headers.GetValues("Retry-After").FirstOrDefault() ?? "30";
                        Console.WriteLine($"[Spotify] Pausing for {retryAfterHeader} seconds.");
                        await Task.Delay(TimeSpan.FromSeconds(Int32.Parse(retryAfterHeader)));
                        return ("SpotifyRateLimitHit", "ErrorException", 0);
                    }

                    return ("UnknownResponseError", "ErrorException", 0);
                }



            }
            

        }


        public async Task<Scrobble> GetScrobbleData(Scrobble ScrobbleIn, string authToken)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var (t, a, ms) = await SearchForTrack(ScrobbleIn.TrackName, ScrobbleIn.ArtistName, authToken);

                // If the rate limit is hit, run the track request that hit it one more time so that no track is excluded.
                while (t == "SpotifyRateLimitHit" || a == "ErrorException")
                {
                    (t, a, ms) = await SearchForTrack(ScrobbleIn.TrackName, ScrobbleIn.ArtistName, authToken);
                }


                // Check if the track data was retrieved from Spotify or from the DB cache.
                
                //Console.WriteLine($"[Spotify] Found a new track '{t}' by '{a}' with an ms runtime of {ms.ToString()}");

                ScrobbleIn.Runtime = ms;
                ScrobbleIn.TotalRuntime = ms * ScrobbleIn.Count;

                return ScrobbleIn;

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
