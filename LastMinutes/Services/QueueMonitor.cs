using LastMinutes.Data;
using LastMinutes.Models;
using LastMinutes.Models.LastFM;
using LastMinutes.Models.LMData;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace LastMinutes.Services
{
    public class QueueMonitor : BackgroundService
    {

        private readonly ILastFMGrabber _lastfm;
        private readonly ISpotifyGrabber _spotify;
        private readonly IMusicBrainz _mb;
        private readonly IDeezerGrabber _deezer;
        private readonly IServiceProvider _serviceProvider;

        public QueueMonitor(ILastFMGrabber lastfm, ISpotifyGrabber spotify, IServiceProvider serviceProvider, IMusicBrainz mb, IDeezerGrabber deezer)
        {
            _lastfm = lastfm;
            _serviceProvider = serviceProvider;
            _spotify = spotify;
            _mb = mb;
            _deezer = deezer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

               /* string Token = await _spotify.GetAccessToken();

                Console.WriteLine($"[Debug] Token: {Token}");*/

                await CheckDatabase();
                // await TestingMB();
               
                /*string S1 = "Luke Kearney";
                string S2 = "Luke Wayne";
                int Similatrity = CompareStrings(S1.ToUpper(), S2.ToUpper());
                Console.WriteLine($"String similarity is {Similatrity}");
               */


            }
        }


        private async Task CheckDatabase()
        {

            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                int QueueLength = _lmdata.Queue.Count();

                if (QueueLength > 0)
                {
                    Console.WriteLine($"[Queue] Current queue size is {QueueLength.ToString()}, beginning processing...");
                    Models.LMData.Queue item = _lmdata.Queue.FirstOrDefault();
                    if (item != null)
                    {

                        #region Get Mode Strings

                        string to = "";
                        string from = "";
                        string TimeFrame = "";

                        switch (item.Mode)
                        {
                            case 1:
                                // All time
                                TimeFrame = "all time";
                                break;
                            case 2:
                                // Last week
                                TimeFrame = "the last week";
                                from = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds().ToString();
                                to = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                                break;
                            case 3:
                                // Last month
                                TimeFrame = "the last month";
                                from = DateTimeOffset.UtcNow.AddMonths(-1).ToUnixTimeSeconds().ToString();
                                to = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                                break;
                            case 4:
                                // Last year
                                TimeFrame = "the last year";
                                from = DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeSeconds().ToString();
                                to = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                                break;
                            case 5:
                                // This week
                                TimeFrame = "this week";
                                from = DateTimeOffset.UtcNow.AddDays(-DateTimeOffset.UtcNow.DayOfWeek.GetHashCode()).ToUnixTimeSeconds().ToString();
                                to = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                                break;
                            case 6:
                                // This month
                                TimeFrame = "this month";
                                from = DateTimeOffset.UtcNow.AddDays(-DateTimeOffset.UtcNow.Day).ToUnixTimeSeconds().ToString();
                                to = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                                break;
                            case 7:
                                // This year
                                TimeFrame = "this year";
                                from = DateTimeOffset.UtcNow.AddDays(-DateTimeOffset.UtcNow.DayOfYear).ToUnixTimeSeconds().ToString();
                                to = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                                break;
                            default:
                                TimeFrame = "all time";
                                from = DateTimeOffset.UtcNow.AddMonths(-1).ToUnixTimeSeconds().ToString();
                                break;
                        }
                        #endregion

                        await UpdateStatus(item, "Beginning processing...");

                        // Populate 
                        List<Dictionary<string, string>> AllScrobbles = new List<Dictionary<string, string>>();

                        LastFMUserData LastFMUser = await _lastfm.GetUserData(item.Username);
                        int TotalLastFmPages = await _lastfm.GetTotalPages(item.Username, to, from);
                        int MaxRequests = 25;


                        if (LastFMUser.User != null)
                        {
                            #region Last.FM Scrobble Grabbing

                            var tasks = new List<Task<List<Dictionary<string, string>>>>();

                            for (int page = 1; page <= TotalLastFmPages; page++)
                            {
                                tasks.Add(_lastfm.FetchScrobblesPerPage(item.Username, page, to, from));
                                // Limit the amount of concurrent requests.
                                if (tasks.Count >= MaxRequests || page == TotalLastFmPages)
                                {
                                    // Await all tasks if we reached the maximum or if it's the last page.
                                    var batchResults = await Task.WhenAll(tasks);

                                    // Combine results from all tasks in htis batch
                                    foreach (var result in batchResults)
                                    {
                                        AllScrobbles.AddRange(result);
                                        if (result.Count == 0)
                                        {
                                            Console.WriteLine("[Queue] There was a request error to Last.FM. Some scrobbles may be missing!");
                                        }
                                    }
                                    Console.WriteLine($"[Queue] Loaded Last.FM up to pages {page.ToString()}/{TotalLastFmPages.ToString()}");
                                    await UpdateStatus(item, $"Found Last.FM page {page}/{TotalLastFmPages}");

                                    tasks.Clear();

                                }
                            }

                            // Create dictionary for scrobbles with their total count 
                            Dictionary<(string trackName, string artistName), int> ScrobblesAccumulatedPlays = _lastfm.AccumulateTrackPlays(AllScrobbles);
                            int TotalTracks = ScrobblesAccumulatedPlays.Count();

                            List<Scrobble> AccumulatedScrobbles = new List<Scrobble>();
                            foreach (var dd in ScrobblesAccumulatedPlays)
                            {
                                AccumulatedScrobbles.Add(new Scrobble()
                                {
                                    TrackName = dd.Key.trackName,
                                    ArtistName = dd.Key.artistName,
                                    Count = dd.Value
                                });
                            }

                            Console.WriteLine($"[Queue] {item.Username} - Account Scrobbles: {LastFMUser.User.Playcount}");
                            Console.WriteLine($"[Queue] {item.Username} - Loaded Scrobbles: {AllScrobbles.Count()}");
                            Console.WriteLine($"[Queue] {item.Username} - Loaded Tracks: {ScrobblesAccumulatedPlays.Count()}");
                            Console.WriteLine($"[Queue] {item.Username} - Loaded Tracks: {ScrobblesAccumulatedPlays.Count()}");






                            #endregion

                            #region Cache Loading


                            await UpdateStatus(item, $"Finding cached tracks...");

                            List<Tracks> AllCached = await _lmdata.Tracks.ToListAsync();
                            List<Scrobble> ProcessedScrobbles = new List<Scrobble>();
                            List<Scrobble> UncachedScrobbles = new List<Scrobble>();


                            foreach (Scrobble SearchFor in AccumulatedScrobbles)
                            {
                                Tracks found = AllCached.FirstOrDefault(x =>
                                                                            string.Equals(x.Name, SearchFor.TrackName, StringComparison.OrdinalIgnoreCase) &&
                                                                            string.Equals(x.Artist, SearchFor.ArtistName, StringComparison.OrdinalIgnoreCase));

                                if (found != null)
                                {
                                    ProcessedScrobbles.Add(new Scrobble()
                                    {
                                        TrackName = SearchFor.TrackName,
                                        ArtistName = SearchFor.ArtistName,
                                        Count = SearchFor.Count,
                                        Runtime = found.Runtime,
                                        TotalRuntime = found.Runtime * SearchFor.Count
                                    });
                                }
                                else
                                {
                                    UncachedScrobbles.Add(new Scrobble()
                                    {
                                        TrackName = SearchFor.TrackName,
                                        ArtistName = SearchFor.ArtistName,
                                        Count = SearchFor.Count
                                    });
                                }
                            }

                            #endregion


                            Console.WriteLine($"[Queue] Total cached tracks: {ProcessedScrobbles.Count}");
                            Console.WriteLine($"[Queue] Total uncached scrobbles: {UncachedScrobbles.Count}");


                            #region Metadata Grabbing

                            #region Last.FM
                            await UpdateStatus(item, $"Searching minutes on Last.FM for {UncachedScrobbles.Count} tracks.");

                            // Try get scrobble runtime from Last.FM

                            var ScrobbleTasks = new List<Task<Scrobble>>();
                            foreach (Scrobble sgd in UncachedScrobbles)
                            {
                                var task = _lastfm.GetScrobbleLength(sgd);
                                ScrobbleTasks.Add(task);
                            }

                            await Task.WhenAll(ScrobbleTasks);

                            foreach (var task in ScrobbleTasks)
                            {
                                Scrobble result = await task;
                                if (result.Runtime != 0)
                                {
                                    ProcessedScrobbles.Add(result);
                                    UncachedScrobbles.Remove(result);
                                }
                            }

                            Console.WriteLine($"[Queue] Last.FM search complete. Remaining uncached scrobbles: {UncachedScrobbles.Count}");

                            await Task.Delay(TimeSpan.FromSeconds(3));

                            #endregion


                            // Search Deezer for all things scrobbles
                            #region Deezer
                            await UpdateStatus(item, $"Searching minutes on Deezer for {UncachedScrobbles.Count} tracks.");
                            var DeezerTasks = new List<Task<Scrobble>>();
                            foreach (Scrobble sgd in UncachedScrobbles)
                            {
                                var task = _deezer.GetScrobbleData(sgd);
                                DeezerTasks.Add(task);
                            }

                            await Task.WhenAll(DeezerTasks);

                            foreach (var task in DeezerTasks)
                            {
                                Scrobble result = await task;
                                if (result.Runtime != 0)
                                {
                                    ProcessedScrobbles.Add(result);
                                    UncachedScrobbles.Remove(result);
                                }
                            }

                            Console.WriteLine($"[Queue] Deezer search complete. Remaining uncached scrobbles: {UncachedScrobbles.Count}");
                            #endregion


                            //Get Spotify auth token
                            #region Spotify Auth Token

                            string SpotifyAuthToken = await _spotify.GetAccessToken();
                            while (SpotifyAuthToken == "tokenfailure")
                            {
                                Console.WriteLine("[Spotify] Failed to retrieve an auth token from Spotify. Retrying...");
                                await Task.Delay(TimeSpan.FromSeconds(5));
                                SpotifyAuthToken = await _spotify.GetAccessToken();
                            }
                            #endregion


                            // Search Spotify for all things scrobbles
                            #region Spotify
                            await UpdateStatus(item, $"Searching minutes on Spotify for {UncachedScrobbles.Count} tracks.");
                            var SpotifyTasks = new List<Task<Scrobble>>();
                            foreach (Scrobble sgd in UncachedScrobbles)
                            {
                                var task = _spotify.GetScrobbleData(sgd, SpotifyAuthToken);
                                SpotifyTasks.Add(task);
                            }

                            await Task.WhenAll(SpotifyTasks);

                            foreach (var task in SpotifyTasks)
                            {
                                Scrobble result = await task;
                                if (result.Runtime != 0)
                                {
                                    ProcessedScrobbles.Add(result);
                                    UncachedScrobbles.Remove(result);
                                }
                            }

                            Console.WriteLine($"[Queue] Spotify search complete. Remaining uncached scrobbles: {UncachedScrobbles.Count}");

                            #endregion


                            // Search MusicBrainz for all things scrobbles
                            #region MusicBrainz
                            /*var MusicBrainzTasks = new List<Task<Scrobble>>();

                            foreach (Scrobble sgd in UncachedScrobbles)
                            {
                                var task = _mb.GetScrobbleData(sgd);
                                MusicBrainzTasks.Add(task);
                            }

                            await Task.WhenAll(MusicBrainzTasks);

                            foreach (var task in MusicBrainzTasks)
                            {
                                Scrobble result = await task;
                                if (result.Runtime != 0)
                                {
                                    ProcessedScrobbles.Add(result);
                                    UncachedScrobbles.Remove(result);
                                }
                            }

                            Console.WriteLine($"[Queue] MusicBrainz search complete. Remaining uncached scrobbles: {UncachedScrobbles.Count}");
                            await Task.Delay(TimeSpan.FromSeconds(3));*/
                            #endregion


                            #endregion


                            Console.WriteLine($"[Queue] Total Calculated Tracks: {ProcessedScrobbles.Count}");
                            await UpdateStatus(item, $"Searching complete. Could not find minutes for {UncachedScrobbles.Count} tracks.");

                            long TotalRuntime = 0;

                            // Calculate total runtime
                            foreach (Scrobble ctr in ProcessedScrobbles)
                            {
                                TotalRuntime += ctr.TotalRuntime;
                            }

                            Console.WriteLine($"[Queue] Total Calculated Minutes: {_spotify.ConvertMsToMinutes(TotalRuntime)} minutes ({TotalRuntime})");

                            List<Scrobble> TopScrobbles = ProcessedScrobbles
                                .OrderByDescending(kvp => kvp.TotalRuntime)
                                .Take(25)
                                .ToList();

                            List<Scrobble> BadScrobbles = UncachedScrobbles
                                .Where(kvp => kvp.Count > 3)
                                .OrderByDescending(kvp => kvp.Count)
                                .Take(25)
                                .ToList();

                            /* foreach (var Song in TopTenMinutes)
                             {
                                 Console.WriteLine($"[Queue] BadScrobble: {Song.TrackName} by {Song.ArtistName}, count of {Song.Count}");
                             }*/

                            Models.LMData.Results Result = new Models.LMData.Results()
                            {
                                Username = item.Username,
                                TotalPlaytime = TotalRuntime,
                                AllScrobbles = JsonConvert.SerializeObject(TopScrobbles),
                                BadScrobbles = JsonConvert.SerializeObject(BadScrobbles),
                                Created_On = DateTime.Now,
                                TimeFrame = TimeFrame
                            };

                            if (from != string.Empty && to != string.Empty)
                            {
                                Result.FromWhen = DateTimeOffset.FromUnixTimeSeconds(int.Parse(from)).DateTime;
                                Result.ToWhen = DateTimeOffset.FromUnixTimeSeconds(int.Parse(to)).DateTime;
                            }

                            await RemoveResults(item.Username);

                            _lmdata.Results.Add(Result);

                            await _lmdata.SaveChangesAsync();

                            await ClearFromQueue(item);

                        }
                        else
                        {
                            await ClearFromQueue(item);
                        }
                    }

                }
                else
                {
                    //Console.WriteLine("[Queue] Queue is currently empty.");
                }

            }
        }



        private async Task<bool> UpdateStatus(Models.LMData.Queue item, string Status)
        {
            if (item == null) { return false; }
            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                item.Status = Status;
                _lmdata.Queue.Update(item);
                if (await _lmdata.SaveChangesAsync() > 0)
                {
                    return true;
                } else
                {
                    return false;
                }
            }
        }


        private async Task<bool> ClearFromQueue(Models.LMData.Queue item)
        {
            if (item == null) { return false; }
            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                _lmdata.Queue.Remove(item);

                if (await _lmdata.SaveChangesAsync() > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }

            }
        }

        private async Task<bool> RemoveResults(string Username)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                IQueueManager _queue = scope.ServiceProvider.GetService<IQueueManager>();

                if (await _queue.RemoveResults(Username))
                {
                    return true;
                } else
                {
                    return false;
                }

            }
        }


        


    }
}
