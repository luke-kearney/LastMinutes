using LastMinutes.Data;
using LastMinutes.Models;
using LastMinutes.Models.LMData;
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

                        // Populate 
                        List<Dictionary<string, string>> AllScrobbles = new List<Dictionary<string, string>>();

                        int TotalLastFmPages = await _lastfm.GetTotalPages(item.Username);
                        int MaxRequests = 25;


                        #region Last.FM Scrobble Grabbing

                        var tasks = new List<Task<List<Dictionary<string, string>>>>();

                        for (int page = 1; page <= TotalLastFmPages; page++)
                        {
                            tasks.Add(_lastfm.FetchScrobblesPerPage(item.Username, page));
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

                        Console.WriteLine($"[Queue] {item.Username} - Total Scrobbles: {AllScrobbles.Count().ToString()}");
                        Console.WriteLine($"[Queue] {item.Username} - Total Tracks: {ScrobblesAccumulatedPlays.Count().ToString()}");
                        await Task.Delay(TimeSpan.FromSeconds(5));

                        #endregion

                        #region Cache Loading

                        List<Tracks> AllCached = await _lmdata.Tracks.ToListAsync();
                        List<Scrobble> ProcessedScrobbles = new List<Scrobble>();
                        List<Scrobble> UncachedScrobbles = new List<Scrobble>();


                        foreach (Scrobble SearchFor in AccumulatedScrobbles)
                        {
                            Tracks found = AllCached.FirstOrDefault(x => x.Name == SearchFor.TrackName && x.Artist == SearchFor.ArtistName);
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
                        await Task.Delay(TimeSpan.FromSeconds(5));


                        #region Metadata Grabbing

                        // Search Deezer for all things scrobbles
                        #region Deezer
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
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        #endregion


                        //Get Spotify auth token

                        #region Spotify Auth Token
                        /*
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
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        */
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
                        await Task.Delay(TimeSpan.FromSeconds(10));*/
                        #endregion


                        #endregion


                        Console.WriteLine($"[Queue] Total Calculated Tracks: {ProcessedScrobbles.Count}");

                        long TotalRuntime = 0;

                        // Calculate total runtime
                        foreach (Scrobble ctr in ProcessedScrobbles)
                        {
                            TotalRuntime += ctr.TotalRuntime;
                        }

                        Console.WriteLine($"[Queue] Total Calculated Minutes: {_spotify.ConvertMsToMinutes(TotalRuntime)} minutes ({TotalRuntime})");

                        var TopTenMinutes = UncachedScrobbles
                            .OrderByDescending(kvp => kvp.Count)
                            .Take(10)
                            .ToList();

                        foreach (var Song in TopTenMinutes)
                        {
                            Console.WriteLine($"[Queue] BadScrobble: {Song.TrackName} by {Song.ArtistName}, count of {Song.Count}");
                        }

                        Models.LMData.Results Result = new Models.LMData.Results()
                        {
                            Username = item.Username,
                            TotalPlaytime = TotalRuntime,
                            AllScrobbles = JsonConvert.SerializeObject(ProcessedScrobbles.OrderByDescending(kvp => kvp.TotalRuntime).Take(25).ToList()),
                            Created_On = DateTime.Now
                        };

                        _lmdata.Results.Add(Result);

                        await _lmdata.SaveChangesAsync();

                        await ClearFromQueue(item);

                    }


                }
                else
                {
                    Console.WriteLine("[Queue] Queue is currently empty.");
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


        


    }
}
