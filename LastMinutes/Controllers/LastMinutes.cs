using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Xml.Linq;

namespace LastMinutes.Controllers
{
    public class LastMinutes : Controller
    {
        private string apiUrl = "http://ws.audioscrobbler.com/2.0/";
        private string apiKey = "8fdac1b1a6e18a0d4eb235e263ab33cb";

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
                    }

                    // Clear the tasks list for the next batch
                    tasks.Clear();
                }
            }

            Console.WriteLine("Collection Complete!");
            Console.WriteLine($"Total scrobbles amassed: {allScrobbles.Count.ToString()}");

            string output = "";

            // Process all scrobbles
            foreach (var scrobble in allScrobbles)
            {
                output += $"A: {scrobble["artist"]} | T: {scrobble["name"]} <br>";
            }

            output += $"<br><br>Total Scrobbles Loaded: {allScrobbles.Count.ToString()}";
            return Content(output, "text/html");
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
                        Dictionary<string, string> scrobbleDict = new Dictionary<string, string>();
                        scrobbleDict.Add("artist", trackElement.Element("artist").Value);
                        scrobbleDict.Add("name", trackElement.Element("name").Value);
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

    }
}
