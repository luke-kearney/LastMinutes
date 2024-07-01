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
        private readonly ILogger<SpotifyGrabber> _logger;
        private readonly IConfiguration _config;
        private readonly ICacheManager _cache;

        private string SpotifyApiUrl = string.Empty;
        private string SpotifyAccUrl = string.Empty;
        private string SpotifyClientId = string.Empty;
        private string SpotifyClientSecret = string.Empty;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(2);

        public SpotifyGrabber(
            IServiceProvider serviceProvider,
            ILogger<SpotifyGrabber> logger,
            IConfiguration config,
            ICacheManager cache) 
        { 
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;
            _cache = cache;

            SpotifyApiUrl = config.GetValue<string>("SpotifyApiUrl") ?? "";
            SpotifyAccUrl = config.GetValue<string>("SpotifyAccUrl") ?? "";
            SpotifyClientId = config.GetValue<string>("SpotifyClientId") ?? "";
            SpotifyClientSecret = config.GetValue<string>("SpotifyClientSecret") ?? "";
        }


        public async Task<(string trackName, string artistName, int durationMs)> SearchForTrack(string trackName, string artistName, string authToken)
        {
            
            HttpClient httpClient = new HttpClient();

            string encodedTrackName = Uri.EscapeDataString(trackName);
            string encodedArtistName = Uri.EscapeDataString(artistName);


            string searchUrl = $"{SpotifyApiUrl}/search?q={encodedTrackName}+artist:{encodedArtistName}&type=track&limit=4";

            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            try
            {
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
                                AddedByResult_ArtistName = artistNameResult,
                                AddedByResult_Title = trackNameResult,
                                Source = "Spotify" 
                            };

                            if (await _cache.AddTrackToCache(CacheTrack))
                                _logger.LogInformation("[Spotify] Track {TrackName} by {ArtistName} was added to the cache with a runtime of {Runtime}ms", trackName, artistName, durationMsResult);

                                

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

                    //Console.WriteLine($"[Spotify] Error occurred while asking Spotify about '{trackName}' by {artistName}..");
                    //Console.WriteLine($"[Spotify] Error is {response.StatusCode}");

                    // if the response is TooManyRequests, retrieve the cool-down time and wait for that amount before returning an empty/exception variable.
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        string retryAfterHeader = response.Headers.GetValues("Retry-After").FirstOrDefault() ?? "30";
                        _logger.LogWarning("[Spotify] Spotify requests paused for {PauseDuration} seconds.", retryAfterHeader);
                        await Task.Delay(TimeSpan.FromSeconds(Int32.Parse(retryAfterHeader)));
                        return ("SpotifyRateLimitHit", "ErrorException", 0);
                    }

                    return ("UnknownResponseError", "ErrorException", 0);
                }
            } catch (Exception ex)
            {
                _logger.LogCritical(ex, "[Spotify] An unknown exception just occurred on SearchForTrack..");
                return ("UnknownResponseError", "ErrorException", 0);
            }
        }


        public async Task<Scrobble> GetScrobbleData(Scrobble ScrobbleIn, string authToken)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                var (t, a, ms) = await SearchForTrack(ScrobbleIn.TrackName, ScrobbleIn.ArtistName, authToken);
                int RetriesCount = 0;
                // If the rate limit is hit, run the track request that hit it one more time so that no track is excluded.
                while (t == "SpotifyRateLimitHit" || a == "ErrorException" && RetriesCount > 5)
                {
                    _logger.LogError("[Spotify] 'SpotifyRateLimitHit' or 'ErrorException' or 'UnknownResponseError' for track {TrackName} by {ArtistName}. Retrying {Retries}/5", ScrobbleIn.TrackName, ScrobbleIn.ArtistName, RetriesCount);
                    (t, a, ms) = await SearchForTrack(ScrobbleIn.TrackName, ScrobbleIn.ArtistName, authToken);
                    RetriesCount++;
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
