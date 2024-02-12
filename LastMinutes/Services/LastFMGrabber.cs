using LastMinutes.Data;
using LastMinutes.Models.LastFM;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Linq;

namespace LastMinutes.Services
{

    public interface ILastFMGrabber
    {

        public Task<LastFMUserData> GetUserData(string username);

        public Task<int> GetTotalPages(string username, string to, string from);

        public Task<List<Dictionary<string, string>>> FetchScrobblesPerPage(string username, int page, string to, string from);

        public Dictionary<(string, string), int> AccumulateTrackPlays(List<Dictionary<string, string>> AllScrobbles); 

    }


    public class LastFMGrabber : ILastFMGrabber
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        private string LastFMApiUrl = string.Empty;
        private string LastFMApiKey = string.Empty;

        public LastFMGrabber(
            IServiceProvider serviceProvider,
            IConfiguration config) 
        { 
            _serviceProvider = serviceProvider;
            _config = config;   

            LastFMApiUrl = config.GetValue<string>("LastFMApiUrl");
            LastFMApiKey = config.GetValue<string>("LastFMApiKey");

        }

        public async Task<LastFMUserData> GetUserData(string username)
        {
            string url = $"{LastFMApiUrl}?method=user.getinfo&user={username}&api_key={LastFMApiKey}&format=json";

            using (HttpClient client = new HttpClient())
            {

                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    LastFMUserData data = JsonConvert.DeserializeObject<LastFMUserData>(responseBody) ?? new LastFMUserData() { Success = false, ErrorMessage = "Unknown error occurred." };
                    return data;
                } else
                {
                    return new LastFMUserData() { Success = false, ErrorMessage = $"Request error message: {response.StatusCode.ToString()}" };
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
                while (!response.IsSuccessStatusCode && Retries > 5) 
                {
                    response = await client.GetAsync(url);
                    Retries++;
                    Console.WriteLine($"[LastFM] {username} - error occurred while pulling scrobbles, retrying... ({Retries} / 5)");
                }

                if (response.IsSuccessStatusCode)
                {
                    string responsebody = await response.Content.ReadAsStringAsync();
                    XDocument doc = XDocument.Parse(responsebody);

                    List<Dictionary<string, string>> scrobblesPerPage = new List<Dictionary<string, string>>();

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
                } else
                {
                    Console.WriteLine($"[LastFM] {username} - error occurred while pulling scrobbles, all retries failed..");
                    return new List<Dictionary<string, string>>();
                }

            }
        }

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
                    Console.WriteLine($"Error: {response.StatusCode}");
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
