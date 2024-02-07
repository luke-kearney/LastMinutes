﻿using LastMinutes.Data;
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
        private readonly IServiceProvider _serviceProvider;

        public QueueMonitor(ILastFMGrabber lastfm, ISpotifyGrabber spotify, IServiceProvider serviceProvider)
        {
            _lastfm = lastfm;
            _serviceProvider = serviceProvider;
            _spotify = spotify;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

                await CheckDatabase();

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

                                tasks.Clear();

                            }
                        }

                        // Create dictionary for scrobbles with their total count 
                        Dictionary<(string trackName, string artistName), int> ScrobblesAccumulatedPlays = _lastfm.AccumulateTrackPlays(AllScrobbles);

                        int TotalTracks = ScrobblesAccumulatedPlays.Count();

                        Console.WriteLine($"[Queue] {item.Username} - Total Scrobbles: {AllScrobbles.Count().ToString()}");
                        Console.WriteLine($"[Queue] {item.Username} - Total Tracks: {ScrobblesAccumulatedPlays.Count().ToString()}");

                        var SpotifyTasks = new List<Task<Dictionary<(string, string), int>>>();
                        Dictionary<(string, string), int> SpotifyMinutes = new Dictionary<(string, string), int>();

                        string SpotifyToken = await _spotify.GetAccessToken();

                        foreach (var entry in ScrobblesAccumulatedPlays)
                        {
                            var task = _spotify.GetTrackRuntime(entry.Key.trackName, entry.Key.artistName, entry.Value, SpotifyToken);
                            SpotifyTasks.Add(task);
                        }

                        await Task.WhenAll(SpotifyTasks);

                        foreach (var task in SpotifyTasks)
                        {
                            var runtime = await task;
                            foreach (var kvp in runtime)
                            {
                                SpotifyMinutes[kvp.Key] = kvp.Value;
                            }
                        }

                        Console.WriteLine($"[Queue] Spotify Calculated Tracks: {SpotifyMinutes.Count}");

                        int TotalRuntime = 0; 

                        foreach (var Song in SpotifyMinutes)
                        {
                            TotalRuntime += Song.Value;
                        }

                        Console.WriteLine($"[Queue] Spotify Calculated Minutes: {_spotify.ConvertMsToMinutes(TotalRuntime)} minutes ({TotalRuntime})");

                        var TopTenMinutes = SpotifyMinutes
                            .OrderByDescending(kvp => kvp.Value)
                            .Take(10)
                            .ToList();

                        foreach (var Song in TopTenMinutes)
                        {
                            Console.WriteLine($"[Queue] Song: {Song.Key.Item1} by {Song.Key.Item2}, total listening time: {_spotify.ConvertMsToMinutes(Song.Value)} minutes");
                        }

                        Models.LMData.Results Result = new Models.LMData.Results()
                        {
                            Username = item.Username,
                            TotalPlaytime = TotalRuntime,
                            AllScrobbles = JsonConvert.SerializeObject(SpotifyMinutes),
                            Created_On = DateTime.Now
                        };

                        _lmdata.Results.Add(Result);

                        await _lmdata.SaveChangesAsync();

                        await ClearFromQueue(item);

                    }


                } else
                {
                    Console.WriteLine("[Queue] Queue is currently empty.");
                }

            }
        }



        private async Task<bool> ClearFromQueue(Models.LMData.Queue item)
        {
            if (item == null) { return false;  }
            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                _lmdata.Queue.Remove(item);

                if (await _lmdata.SaveChangesAsync() > 0){
                    return true;
                } else
                {
                    return false;
                }

            }
        }

        
        
    }
}
