using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace LastMinutes.Controllers
{
    public class LastMinutes : Controller
    {
        private string apiUrl = "http://ws.audioscrobbler.com/2.0/";
        private string apiKey = "8fdac1b1a6e18a0d4eb235e263ab33cb";

        private string SpotifyApiUrl = "https://api.spotify.com/v1";
        private string SpotifyTokenUrl = "https://accounts.spotify.com/api/token";
        private string SpotifyClientID = "ba03d074797d4f2787ebd4210c99a484";
        private string SpotifyClientSecret = "11d92e7d3c1a44b69f95a0a0dd324923";
        private string SpotifyToken = "";

        public IActionResult Index()
        {
            return View("LandingPage");
        }


        [HttpPost]
        [Route("/LastMinutes/CheckMinutes")]
        public async Task<IActionResult> CheckMinutes(IFormCollection col)
        {
            string Username = col["username"].ToString();  
            if (string.IsNullOrEmpty(Username) ) { return Content("No Username Supplied!"); }

            List<Dictionary<string, string>> allScrobbles = new List<Dictionary<string, string>>();

            int totalPages = await GetTotalPagesAsync(apiUrl, apiKey, Username);
            int maxRequests = 25; // Maximum number of concurrent requests
            var tasks = new List<Task<List<Dictionary<string, string>>>>();
             
            Console.WriteLine($"Total Pages: {totalPages}");

            for (int page = 1; page <= totalPages; page++)
            {
                tasks.Add(FetchScrobblesPerPageAsync(apiUrl, apiKey, Username, page));

                // Limit the number of concurrent requests
                if (tasks.Count >= maxRequests || page == totalPages)
                {
                    // Await all tasks if we reached the maximum or it's the last page
                    var batchResults = await Task.WhenAll(tasks);
                    Console.WriteLine($"Completed Pages: {page}");

                    // Combine results from all tasks in this batch
                    foreach (var result in batchResults)
                    {
                        allScrobbles.AddRange(result);
                        if (result.Count == 0)
                        {
                            Console.WriteLine("There was a request error- some scrobbles may be missing");
                        }
                    }

                    // Clear the tasks list for the next batch
                    tasks.Clear();
                }
            }

            Console.WriteLine("Collection Complete!");
            Console.WriteLine($"Total scrobbles amassed: {allScrobbles.Count.ToString()}");

            string output = "";

            Dictionary<(string Artist, string Track), int> CombinedScrobbles = GetTrackCounts(allScrobbles);
            var SortedScrobbles = CombinedScrobbles.OrderByDescending(entry => entry.Value).ToList();

            Console.WriteLine($"Total tracks scrobbled: {SortedScrobbles.Count.ToString()}");

            Dictionary<(string Artist, string Track), int> TrackRuntimes = new Dictionary<(string Artist, string Track), int>();
            int TotalRuntime = 0;
            var runtimeTasks = new List<Task<Dictionary<(string Artist, string Track), int>>>();

            // Setup spotify
            SpotifyToken = await GetAccessToken(SpotifyClientID, SpotifyClientSecret);
            Console.WriteLine($"Logged into Spotify! Access Token: {SpotifyToken}");

            foreach (var entry in SortedScrobbles)
            {
                var task = GetTrackRuntime(entry.Key.Artist, entry.Key.Track, entry.Value);
                runtimeTasks.Add(task);
            }

            await Task.WhenAll(runtimeTasks);

            foreach (var task in runtimeTasks)
            {
                var runtime = await task;
                foreach (var kvp in runtime)
                {
                    TrackRuntimes[kvp.Key] = kvp.Value;
                }
            }

            // Process all scrobbles
            foreach (var scrobble in SortedScrobbles)
            {
                var Track = TrackRuntimes[(scrobble.Key.Artist, scrobble.Key.Track)];
                string TrackRuntimeString = ConvertMsToMinutes(Track);
                output += $"A: {scrobble.Key.Artist} | T: {scrobble.Key.Track} | T: {TrackRuntimeString} minutes<br>";
            }

            

            string SpotifyOutput = $"Total Time Listening to Music: {ConvertMsToHoursMinutes(TotalRuntime)} <br><br>";
            

            output += $"<br><br>Total Scrobbles Loaded: {allScrobbles.Count.ToString()}";
            output += $"<br><br>Total Tracks Scrobbled: {SortedScrobbles.Count.ToString()}";

            output = SpotifyOutput + "<br><br><br>Scrobbles: <br>" + output;



            return Content(output, "text/html");
        }


        #region Last.FM Stuff

        static Dictionary<(string Artist, string Track), int> GetTrackCounts(List<Dictionary<string, string>> allScrobbles)
        {
            Dictionary<(string Artist, string Track), int> trackCounts = new Dictionary<(string Artist, string Track), int>();

            foreach (var scrobble in allScrobbles)
            {
                string artist = scrobble["artist"];
                string name = scrobble["name"];

                var key = (Artist: artist, Track: name);

                if (trackCounts.ContainsKey(key))
                {
                    trackCounts[key]++;
                } else
                {
                    trackCounts.Add(key, 1);
                }
            }

            return trackCounts;

        }


        static async Task<int> GetTotalPagesAsync(string apiUrl, string apiKey, string username)
        {
            string url = $"{apiUrl}?method=user.getRecentTracks&user={username}&api_key={apiKey}&format=json&limit=200";

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    dynamic data = JsonConvert.DeserializeObject(responseBody);
                    return data.recenttracks["@attr"].totalPages;
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    return 0;
                }
            }
        }

        static async Task<List<Dictionary<string, string>>> FetchScrobblesPerPageAsync(string apiUrl, string apiKey, string username, int page)
        {
            string url = $"{apiUrl}?method=user.getRecentTracks&user={username}&api_key={apiKey}&format=xml&limit=200&page={page}";

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    XDocument doc = XDocument.Parse(responseBody);

                    List<Dictionary<string, string>> scrobblesPerPage = new List<Dictionary<string, string>>();

                    foreach (var trackElement in doc.Root.Element("recenttracks").Elements("track"))
                    {
                        var nowPlayingAttribute = trackElement.Attribute("nowplaying");
                        if (nowPlayingAttribute != null && nowPlayingAttribute.Value == "true")
                        {
                            // break out of loop if the track is playing- causes weird issues with track counts
                            continue;
                        }

                        var nameElement = trackElement.Element("name");
                        string name;

                        // Check if the name element exists
                        if (nameElement != null)
                        {
                            // Decode the name text using the appropriate encoding (e.g., ISO-8859-1)
                            byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(nameElement.Value);
                            name = Encoding.UTF8.GetString(bytes);
                        }
                        else
                        {
                            // Handle the case where the name element is missing
                            name = string.Empty;
                        }

                        Dictionary<string, string> scrobbleDict = new Dictionary<string, string>();
                        scrobbleDict.Add("artist", trackElement.Element("artist").Value);
                        scrobbleDict.Add("name", name);
                        scrobblesPerPage.Add(scrobbleDict);
                    }

                    return scrobblesPerPage;
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    return new List<Dictionary<string, string>>();
                }
            }
        }


        #endregion

        #region Spotify Stuff
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(3);

        public async Task<Dictionary<(string Artist, string Track), int>> GetTrackRuntime(string artist, string track, int count)
        {
            await semaphoreSlim.WaitAsync(); // Wait until a semaphore slot is available
            try
            {
                var (trackName, artistName, durationMs) = await GetMostProbableTrack(track, artist);
                var runtimea = new Dictionary<(string Artist, string Track), int>();

                /*if (trackName != track || artistName != artist)
                {
                    durationMs = 0;
                }*/

                Console.WriteLine($"[Spotify] Found track '{track}' by '{artist}' with a runtime (ms) of {durationMs.ToString()}");
                runtimea.Add((artist, track), count * durationMs);
                return runtimea;
            }
            finally
            {
                semaphoreSlim.Release(); // Release the semaphore slot when done
            }
        }

        public string ConvertMsToHoursMinutes(int durationMs)
        {
            // Convert milliseconds to seconds
            int totalSeconds = durationMs / 1000;

            // Calculate total minutes
            int totalMinutes = totalSeconds / 60;

            // Calculate total hours
            int hours = totalMinutes / 60;

            // Calculate remaining minutes
            int minutes = totalMinutes % 60;

            // Return the result in the format "hours:minutes"
            return $"{hours:D2}:{minutes:D2}";
        }

        public string ConvertMsToMinutes(int durationMs)
        {
            // Convert milliseconds to minutes
            int totalMinutes = durationMs / (1000 * 60);

            // Return the result as a string
            return totalMinutes.ToString();
        }

        public async Task<(string trackName, string artistName, int durationMs)> GetMostProbableTrack(string trackName, string artistName)
        {
            HttpClient httpClient = new HttpClient();
            // Encode track name and artist name for URL
            string encodedTrackName = Uri.EscapeDataString(trackName);
            string encodedArtistName = Uri.EscapeDataString(artistName);

            // Construct search query URL
            string searchUrl = $"{SpotifyApiUrl}/search?q={encodedTrackName}+artist:{encodedArtistName}&type=track&limit=1";

            // Add access token to the request headers
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SpotifyToken);

            // Send HTTP GET request to Spotify API
            HttpResponseMessage response = await httpClient.GetAsync(searchUrl);

            if (response.IsSuccessStatusCode)
            {
                // Deserialize JSON response
                string responseBody = await response.Content.ReadAsStringAsync();
                var responseObject = JsonConvert.DeserializeObject<SpotifySearchResponse>(responseBody);

                if (responseObject == null)
                {
                    return (trackName, artistName, 0);
                }

                if (responseObject.Tracks.Items.Count() != 0)
                { 
                    // Extract track details from the response
                    var track = responseObject.Tracks.Items[0];

                    string trackNameResult = track.Name;
                    string artistNameResult = track.Artists[0].Name;
                    int durationMsResult = track.duration_ms;

                    return (trackNameResult, artistNameResult, durationMsResult);
                } else
                {
                    return (trackName, artistName, 0);
                }
                
            }
            else
            {
                // Handle error response from Spotify API
                throw new Exception($"Failed to retrieve track details. Status code: {response.StatusCode}");
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


        public async Task<string> GetAccessToken(string clientId, string clientSecret)
        {
            try
            {
                // Encode client credentials for Authorization header
                var base64AuthHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

                // Create HttpClient instance
                using var httpClient = new HttpClient();

                // Construct request content
                var requestBodyContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                // Add Authorization header
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", base64AuthHeader);

                // Send HTTP POST request to Spotify Accounts Service
                HttpResponseMessage response = await httpClient.PostAsync(SpotifyTokenUrl, requestBodyContent);

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
                    throw new Exception($"Failed to retrieve access token. Status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // Handle exception
                throw new Exception("Failed to retrieve access token.", ex);
            }
        }

        #endregion


    }
}
