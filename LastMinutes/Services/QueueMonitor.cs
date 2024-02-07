using LastMinutes.Data;
using Microsoft.Extensions.Hosting;
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
        private readonly IServiceProvider _serviceProvider;

        public QueueMonitor(ILastFMGrabber lastfm, IServiceProvider serviceProvider)
        {
            _lastfm = lastfm;
            _serviceProvider = serviceProvider;
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
                        Dictionary<(string, string), int> ScrobblesAccumulatedPlays = _lastfm.AccumulateTrackPlays(AllScrobbles);

                        int TotalTracks = ScrobblesAccumulatedPlays.Count();

                        Console.WriteLine($"[Queue] {item.Username} - Total Scrobbles: {AllScrobbles.Count().ToString()}");
                        Console.WriteLine($"[Queue] {item.Username} - Total Tracks: {ScrobblesAccumulatedPlays.Count().ToString()}");

                        var SpotifyTasks = new List<Task<List<Dictionary<(string, string), int>>>>();

                        for (int trackCounter = 0; trackCounter <= TotalTracks ; trackCounter++)
                        {
                        }



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
