using LastMinutes.Data;
using LastMinutes.Models.LastFM;
using LastMinutes.Models;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LastMinutes.Models.LMData;

namespace LastMinutes.Services
{

    public interface ILastFMGrabber
    {

        public Task<GetLastFmUserResponse> GetUserData(string username);

        public Task<int> GetTotalPages(string username, string to, string from);

        public Task<List<Dictionary<string, string>>> FetchScrobblesPerPage(string username, int page, string to, string from);


        public Dictionary<(string, string), int> AccumulateTrackPlays(List<Dictionary<string, string>> AllScrobbles);

        public Task<List<Scrobble>> GetScrobbles(string username, int page);

        public Task<int> GetTopPagesTotal(string username);
    }


    public class LastFMGrabber : ILastFMGrabber
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LastFMGrabber> _logger;
        private readonly IConfiguration _config;

        private string LastFMApiUrl = string.Empty;
        private string LastFMApiKey = string.Empty;

        private bool EnableCaching = false;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(8);

        public LastFMGrabber(
            IServiceProvider serviceProvider,
            ILogger<LastFMGrabber> logger,
            IConfiguration config) 
        { 
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;   

            LastFMApiUrl = config.GetValue<string>("LastFMApiUrl") ?? "";
            LastFMApiKey = config.GetValue<string>("LastFMApiKey") ?? "";
           // EnableCaching = config.GetValue<bool>("EnableCaching");
        }

        public async Task<GetLastFmUserResponse> GetUserData(string username)
        {
            string url = $"{LastFMApiUrl}?method=user.getinfo&user={username}&api_key={LastFMApiKey}&format=json";

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var fetchUserData = JsonConvert.DeserializeObject<FetchLastFmUser>(responseBody);
                    if (fetchUserData != null && fetchUserData.User != null)
                    {
                        return new GetLastFmUserResponse(true, fetchUserData.User);
                    } else if (fetchUserData != null && fetchUserData.User == null)
                    {
                        return new GetLastFmUserResponse(false, errorMessage: "User was not found.", responseCode: 404);
                    }
                    return new GetLastFmUserResponse(false, errorMessage: "Unknown error occurred.", responseCode: Int32.Parse(response.StatusCode.ToString()));
                } else
                {
                    _logger.LogCritical("Failed to get user data for {Username} from LastFM API. Status code: {StatusCode}", username, response.StatusCode);
                    return new GetLastFmUserResponse(false,
                        errorMessage: $"Request error message: {response.StatusCode.ToString()}");
                }

            }
        }

        public async Task<List<Dictionary<string, string>>> FetchScrobblesPerPage(string username, int page, string to, string from)
        {
            string url = $"{LastFMApiUrl}?method=user.getRecentTracks&user={username}&api_key={LastFMApiKey}&from={from}&to={to}&format=xml&limit=200&page={page}";
            if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(from))
            {
                url = $"{LastFMApiUrl}?method=user.getRecentTracks&user={username}&api_key={LastFMApiKey}&format=xml&limit=200&page={page}";
            }

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);
                int Retries = 0;
                while (!response.IsSuccessStatusCode && Retries < 5) 
                {
                    response = await client.GetAsync(url);
                    Retries++;
                    _logger.LogWarning("[LastFM] {Username} - error occurred while pulling scrobbles, retrying... ({Retries} / 5)", username, Retries);
                }

                if (response.IsSuccessStatusCode)
                {
                    List<Dictionary<string, string>> scrobblesPerPage = new List<Dictionary<string, string>>();

                    try
                    {
                        string responsebody = await response.Content.ReadAsStringAsync();
                        XDocument doc = XDocument.Parse(responsebody);
                        
                        foreach (var trackElement in doc.Root.Element("recenttracks").Elements("track"))
                        {
                            var nowPlayingAttribute = trackElement.Attribute("nowplaying");
                            if (nowPlayingAttribute != null && nowPlayingAttribute.Value == "true")
                            {
                                continue;
                            }

                            var nameElement = trackElement.Element("name");
                            string name;

                            if (nameElement != null)
                            {
                                //byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(nameElement.Value);
                                //name = Encoding.UTF8.GetString(bytes);

                                name = nameElement.Value;
                            } else
                            {
                                name = string.Empty;
                            }

                            Dictionary<string, string> scrobbleDict = new Dictionary<string, string>();
                            scrobbleDict.Add("artist", trackElement.Element("artist").Value ?? string.Empty);
                            scrobbleDict.Add("name", name);
                            scrobblesPerPage.Add(scrobbleDict);
                        }

                        return scrobblesPerPage;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical("Failed to parse XML response from LastFM API. Error: {Error}", ex.Message);
                        return scrobblesPerPage;
                    }


                    
                } else
                {
                    _logger.LogError("[LastFM] {Username} - error occurred while pulling scrobbles, all retries failed..", username);
                    return new List<Dictionary<string, string>>();
                }

            }
        }

        

        #region Get Scrobbles WITH runtime

        public async Task<List<Scrobble>> GetScrobbles(string username, int page)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                string url = $"{LastFMApiUrl}?method=user.gettoptracks&api_key={LastFMApiKey}&format=json&user={username}&limit=1000&page={page}&period=overall";


                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(url);

                    int Retries = 0;

                    while (!response.IsSuccessStatusCode && Retries < 5)
                    {
                        response = await client.GetAsync(url);
                        Retries++;
                        _logger.LogWarning("[LastFM] Failed to retrieve top tracks page {Page} for {Username}, retrying... ({Retries} / 5)", page, username, Retries);
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        LastFmTopTracksResponse TopTrackData = JsonConvert.DeserializeObject<LastFmTopTracksResponse>(responseBody)!;
                        List<LastFmTopTrack> TopTracks = TopTrackData.TopTracks.Tracks;

                        List<Scrobble> scrobOut = new List<Scrobble>();

                        foreach (LastFmTopTrack track in TopTracks)
                        {
                            if (!string.IsNullOrEmpty(track.Duration) && track.Duration != "0")
                            {
                                // work out track duration 
                                int PlayCount;
                                int DurationMs;
                                Int32.TryParse(track.Duration, out DurationMs);
                                Int32.TryParse(track.Playcount, out PlayCount);
                                DurationMs = DurationMs * 1000;
                                int TotalDurationMs = DurationMs * PlayCount;

                                Scrobble NewTrack = new Scrobble()
                                {
                                    TrackName = track.Name,
                                    ArtistName = track.Artist.Name,
                                    Count = PlayCount,
                                    Runtime = DurationMs,
                                    TotalRuntime = TotalDurationMs
                                };

                                scrobOut.Add(NewTrack);
                            } else
                            {

                                Int32.TryParse(track.Playcount, out int PlayCount);
                                Scrobble NewTrack = new Scrobble()
                                {
                                    TrackName = track.Name,
                                    ArtistName = track.Artist.Name,
                                    Count = PlayCount,
                                    Runtime = 0,
                                    TotalRuntime = 0
                                };

                                scrobOut.Add(NewTrack);
                            }
                        }

                        return scrobOut;
                    }
                    else
                    {
                        return new List<Scrobble>();
                    }
                }
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }


        public async Task<int> GetTopPagesTotal(string username)
        {
            string url = $"{LastFMApiUrl}?method=user.gettoptracks&api_key={LastFMApiKey}&format=json&user={username}&limit=1000&page=1&period=overall";


            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);

                int Retries = 0;

                while (!response.IsSuccessStatusCode && Retries < 5)
                {
                    response = await client.GetAsync(url);
                    Retries++;
                    _logger.LogWarning("[LastFM] Failed to retrieve total amount of Top Tracks pages for {Username}, retrying... ({Retries} / 5)", username, Retries);
                }

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    LastFmTopTracksResponse TopTrackData = JsonConvert.DeserializeObject<LastFmTopTracksResponse>(responseBody)!;
                    int TotalPages = 0;
                    Int32.TryParse(TopTrackData.TopTracks.Attributes.TotalPages, out TotalPages);
                    return TotalPages;
                }
                else
                {
                    return 0;
                }
            }
        }

        #endregion

        public async Task<int> GetTotalPages(string username, string to, string from)
        {
            string url = $"{LastFMApiUrl}?method=user.getRecentTracks&user={username}&api_key={LastFMApiKey}&from={from}&to={to}&format=json&limit=200";
            if (string.IsNullOrEmpty(to) || string.IsNullOrEmpty(from))
            {
                url = $"{LastFMApiUrl}?method=user.getRecentTracks&user={username}&api_key={LastFMApiKey}&format=json&limit=200";
            }

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
                    _logger.LogError("[LastFM] Error occurred when trying to get total pages for user {Username}. Status code: {StatusCode}", username, response.StatusCode);
                    return 0;
                }
            }

        }

        public Dictionary<(string,string), int> AccumulateTrackPlays(List<Dictionary<string, string>> AllScrobbles)
        {
            Dictionary<(string, string), int> Output = new Dictionary<(string, string), int>();

            foreach (var scrobble in AllScrobbles)
            {
                string artist = scrobble["artist"];
                string name = scrobble["name"];
                var key = (Track: name, Artist: artist);
                if (Output.ContainsKey(key))
                {
                    Output[key]++;
                } else
                {
                    Output.Add(key, 1);
                }
            }
            return Output;
        }


    }
}
