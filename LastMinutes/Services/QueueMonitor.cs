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

        #region Dependancy Injection & Class Constructor

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<QueueMonitor> _logger;
        private readonly ISpotifyGrabber _spotify;
        private readonly IDeezerGrabber _deezer;
        private readonly ILastFMGrabber _lastfm;
        private readonly IMusicBrainz _mb;

        public QueueMonitor(ILastFMGrabber lastfm, 
            IServiceProvider serviceProvider,
            ILogger<QueueMonitor> logger,
            ISpotifyGrabber spotify,
            IDeezerGrabber deezer,
            IMusicBrainz mb)
        {
            _serviceProvider = serviceProvider;
            _spotify = spotify;
            _logger = logger;
            _deezer = deezer;
            _lastfm = lastfm;
            _mb = mb;
        }

        #endregion



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    await CheckDatabase();
                    await ClearOldResults();
                    await ClearOldLeaderboardEntries();
                } catch (Exception e)
                {
                    _logger.LogError(e, "QueueMonitor.cs Experienced a critical fault and the background service has stopped! Restarting service in 30 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
        }



        /// <summary>
        /// This is the main worker method for this application. It processes the queue and retrieves the scrobble data for each user.
        /// </summary>
        /// <returns>Bool</returns>
        private async Task<bool> CheckDatabase()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                LMData lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                int queueLength = lmdata.Queue.Count(x => !x.Failed);

                if (queueLength == 0)
                {
                    _logger.LogInformation("[Queue] Current queue size is 0.");
                    return false;
                }
                
                Models.LMData.Queue? item = lmdata.Queue.FirstOrDefault(x => !x.Failed);

                if (item == null)
                    return false;
                
                
                _logger.LogInformation("[Queue] Current queue size is {QueueLength}, beginning processing with user {Username}", queueLength.ToString(), item.Username);
                
                
                if (item.Retries > 2)
                {
                    _logger.LogWarning("Queue item for user {Username} has been set to failed after too many retries!", item.Username);
                    item.Failed = true;
                    lmdata.Queue.Update(item);
                    await lmdata.SaveChangesAsync();
                    return false;
                }
                

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
                List<Dictionary<string, string>> allScrobbles = new List<Dictionary<string, string>>();

                var fetchLastFmUser = await _lastfm.GetUserData(item.Username);

                if (!fetchLastFmUser.Success)
                {
                    // If error occurs then add to retries before removing from queue.
                    _logger.LogWarning("Queue item for user {Username} has failed to retrieve Last.FM user data. Retrying...", item.Username);
                    item.Retries++;
                    lmdata.Queue.Update(item);
                    await lmdata.SaveChangesAsync();
                    return false;
                }
                
                int totalLastFmPages = await _lastfm.GetTotalPages(item.Username, to, from);
                int maxRequests = 25;


                if (fetchLastFmUser.User == null)
                {
                    await ClearFromQueue(item);
                    return false;
                }

               
                #region Last.FM Scrobble Grabbing
                            
                var tasks = new List<Task<List<Dictionary<string, string>>>>();

                for (int page = 1; page <= totalLastFmPages; page++)
                {
                    tasks.Add(_lastfm.FetchScrobblesPerPage(item.Username, page, to, from));
                    // Limit the amount of concurrent requests.
                    if (tasks.Count >= maxRequests || page == totalLastFmPages)
                    {
                        // Await all tasks if we reached the maximum or if it's the last page.
                        var batchResults = await Task.WhenAll(tasks);

                        // Combine results from all tasks in htis batch
                        foreach (var result in batchResults)
                        {
                            allScrobbles.AddRange(result);
                            if (result.Count == 0)
                            {
                                _logger.LogWarning("[Queue] There was a request error to Last.FM. Some scrobbles may be missing!");
                            }
                        }
                        _logger.LogInformation("[Queue] Loaded Last.FM pages {Step}/{TotalPages}", page.ToString(), totalLastFmPages.ToString());
                        await UpdateStatus(item, $"Found Last.FM page {page}/{totalLastFmPages}");

                        tasks.Clear();

                    }
                }

                // Create dictionary for scrobbles with their total count 
                Dictionary<(string trackName, string artistName), int> ScrobblesAccumulatedPlays = _lastfm.AccumulateTrackPlays(allScrobbles);
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

                #endregion

                _logger.LogInformation("[Queue] {Username} - Account Scrobbles: {AccountScrobbles}. Loaded Scrobbles: {LoadedScrobbles}", item.Username, fetchLastFmUser.User.Playcount, allScrobbles.Count());

                #region Create Holding Objects

                List<Scrobble> ProcessedScrobbles = new List<Scrobble>();
                List<Scrobble> UncachedScrobbles = new List<Scrobble>();
                List<Scrobble> UnfoundScrobbles = new List<Scrobble>();
                List<Scrobble> FoundScrobbles = new List<Scrobble>();

                #endregion


                #region Last.FM Duration Search 

                await UpdateStatus(item, $"Searching on Last.FM for minutes..");
                _logger.LogInformation("[Queue] Beginning search on Last.FM for duration values..");

                int TotalTopPages = await _lastfm.GetTopPagesTotal(item.Username);
                var TopTasks = new List<Task<List<Scrobble>>>();
                for (int i = 1; i <= TotalTopPages; i++)
                {
                    var task = _lastfm.GetScrobbles(item.Username, i);
                    TopTasks.Add(task);
                }

                await Task.WhenAll(TopTasks);
                foreach (var task in TopTasks)
                {
                    List<Scrobble> result = await task;
                    foreach (Scrobble scrob in result)
                    {
                        if (scrob.Runtime != 0)
                        { 
                            FoundScrobbles.Add(scrob);
                        } 

                    }
                }

                foreach (Scrobble Ascrob in AccumulatedScrobbles)
                {

                    Scrobble FoundScrob = FoundScrobbles.Where(x => x.TrackName ==  Ascrob.TrackName && x.ArtistName == Ascrob.ArtistName).FirstOrDefault()!;
                    if (FoundScrob != null)
                    {
                        Ascrob.Runtime = FoundScrob.Runtime;
                        Ascrob.TotalRuntime = FoundScrob.Runtime * Ascrob.Count;
                        ProcessedScrobbles.Add(Ascrob);
                    } else
                    {
                        UnfoundScrobbles.Add(Ascrob);
                    }

                }

                _logger.LogInformation("[Queue] Search on Last.FM has completed.");

                #endregion



                #region Finding Cached Tracks


                await UpdateStatus(item, $"Finding cached tracks...");
                _logger.LogInformation("[Queue] Searching the cache for any unfound durations.");

                
                // Build a distinct set of keys from UnfoundScrobbles
                var scrobbleKeys = UnfoundScrobbles
                    .Select(s => new TrackKey(s.TrackName, s.ArtistName))
                    .Distinct()
                    .ToList();

                var tracks = await lmdata.Tracks
                    .ToListAsync();

                var trackMatches = tracks
                    .Where(t => scrobbleKeys
                        .Any(k => string.Equals(t.Name, k.Name, StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(t.Artist, k.Artist, StringComparison.OrdinalIgnoreCase)))
                    .ToDictionary(
                        t => new TrackKey(t.Name, t.Artist),
                        t => t);

                // Process the scrobbles
                foreach (var searchFor in UnfoundScrobbles)
                {
                    var key = new TrackKey(searchFor.TrackName, searchFor.ArtistName);
                    if (trackMatches.TryGetValue(key, out var found))
                    {
                        ProcessedScrobbles.Add(new Scrobble
                        {
                            TrackName = searchFor.TrackName,
                            ArtistName = searchFor.ArtistName,
                            Count = searchFor.Count,
                            Runtime = found.Runtime,
                            TotalRuntime = found.Runtime * searchFor.Count
                        });
                    }
                    else
                    {
                        UncachedScrobbles.Add(new Scrobble
                        {
                            TrackName = searchFor.TrackName,
                            ArtistName = searchFor.ArtistName,
                            Count = searchFor.Count
                        });
                    }
                }

                
                /*List<Tracks> AllCached = await lmdata.Tracks.ToListAsync();


                foreach (Scrobble SearchFor in UnfoundScrobbles)
                {
                    Tracks found = AllCached.FirstOrDefault(x =>
                                                                string.Equals(x.Name, SearchFor.TrackName, StringComparison.OrdinalIgnoreCase) &&
                                                                string.Equals(x.Artist, SearchFor.ArtistName, StringComparison.OrdinalIgnoreCase))!;

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
                }*/

                _logger.LogInformation("[Queue] Cache search completed. Found {FoundCache} tracks. There are {Unfound} tracks that need to be searched!", ProcessedScrobbles.Count, UncachedScrobbles.Count);

                #endregion


                            
                #region Last.FM- Deprecated
                // I'm sure this code was implemented before I found out that Last.FM provides track runtime data with ONLY the top tracks endpoint.
                            
                /*
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
                */

                #endregion

                          
                
                #region Deezer

                await UpdateStatus(item, $"Searching minutes on Deezer for {UncachedScrobbles.Count} tracks.");
                _logger.LogInformation("[Queue] Beginning search on Deezer for duration values..");

                var DeezerTasks = new List<Task<Scrobble>>();
                foreach (Scrobble sgd in UncachedScrobbles)
                {
                    var task = _deezer.GetScrobbleData(sgd);
                    DeezerTasks.Add(task);
                }

                await Task.WhenAll(DeezerTasks);
                int SetCountTracks = UncachedScrobbles.Count;
                foreach (var task in DeezerTasks)
                {
                    Scrobble result = await task;
                    if (result.Runtime != 0)
                    {
                        ProcessedScrobbles.Add(result);
                        UncachedScrobbles.Remove(result);
                    }
                }

                _logger.LogInformation("[Queue] Search on Deezer has completed. Remaining uncached tracks: {Remaining}", UncachedScrobbles.Count());

                #endregion



                #region Spotify

                // Get the auth token
                /*string SpotifyAuthToken = await _spotify.GetAccessToken();
                int count = 0;
                bool SpotifySearchEnabled = true;
                while (SpotifyAuthToken == "tokenfailure" && count <= 4)
                {
                    count++;
                    _logger.LogWarning("[Queue] Failed to retrieve an auth token from Spotify. Retrying {CurrentAttempt}/{MaxTries}", count, 4);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    SpotifyAuthToken = await _spotify.GetAccessToken();
                }
                
                if (SpotifyAuthToken == "tokenfailure")
                {
                    SpotifySearchEnabled = false;
                     _logger.LogError("[Queue] Failed to retrieve an auth token from Spotify after 5 attempts. Aborting Spotify search.");
                    await UpdateStatus(item, "Failed to retrieve an auth token from Spotify. Aborting Spotify search.");
                }


                // Begin Spotify search
                if (SpotifySearchEnabled)
                {
                    await UpdateStatus(item, $"Searching minutes on Spotify for {UncachedScrobbles.Count} tracks.");
                    _logger.LogInformation("[Queue] Beginning search on Spotify for duration values..");

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

                    _logger.LogInformation("[Queue] Search on Spotify has completed. Remaining uncached tracks: {Remaining}", UncachedScrobbles.Count());
                }*/
                

                #endregion



                _logger.LogInformation("[Queue] Total processed tracks: {ProcessedTracks}", ProcessedScrobbles.Count);  
                await UpdateStatus(item, $"Searching complete. Could not find minutes for {UncachedScrobbles.Count} tracks.");

                long TotalRuntime = 0;

                // Calculate total runtime
                foreach (Scrobble ctr in ProcessedScrobbles)
                {
                    TotalRuntime += ctr.TotalRuntime;
                }

                _logger.LogInformation("[Queue] Total Calculated Minutes: {TotalMinutes} minutes ({TotalMs})", _spotify.ConvertMsToMinutes(TotalRuntime), TotalRuntime);

                List<Scrobble> TopScrobbles = ProcessedScrobbles
                    .OrderByDescending(kvp => kvp.TotalRuntime)
                    .Take(25)
                    .ToList();

                List<Scrobble> BadScrobbles = UncachedScrobbles
                    .Where(kvp => kvp.Count >= 1)
                    .OrderByDescending(kvp => kvp.Count)
                    .Take(100)
                    .ToList();

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

                lmdata.Results.Add(Result);

                if (await lmdata.SaveChangesAsync() > 0)
                {
                    await UpdateStats(Result.TotalPlaytime);
                }

                await SubmitToLeaderboard(item, ConvertMsToMinutesLong(TotalRuntime));

                await ClearFromQueue(item);

                

                return true;
                

            }
        }


        /// <summary>
        /// Clears entries older than 24 hours from the Results table.
        /// </summary>
        /// <param name="Hours">Optional parameter to select how old entries need to be prior to removal.</param>
        /// <returns>Bool</returns>
        private async Task<bool> ClearOldResults(int Hours = 24)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                    var TimeAgo = DateTime.Now.AddHours(Hours * -1);
                    List<LastMinutes.Models.LMData.Results> RemoveResults = _lmdata.Results.Where(x => x.Created_On < TimeAgo).ToList();

                    if (RemoveResults != null)
                    {
                        _lmdata.Results.RemoveRange(RemoveResults);
                        if (await _lmdata.SaveChangesAsync() > 0)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            } catch
            {
                return false;
            }
            

        }


        /// <summary>
        /// Clears entries older than 72 days from the Leaderboard table.
        /// </summary>
        /// <param name="Days">Optional parameter to select how old entries need to be prior to removal.</param>
        /// <returns>Bool</returns>
        private async Task<bool> ClearOldLeaderboardEntries(int Days = 72)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                    var TimeAgo = DateTime.Now.AddDays(Days * -1);
                    var RemoveResults = await _lmdata.Leaderboard.Where(x => x.Created_On < TimeAgo).ToListAsync();

                    _lmdata.Leaderboard.RemoveRange(RemoveResults);
                    if (await _lmdata.SaveChangesAsync() > 0)
                    {
                        return true;
                    } else
                    {
                        return false;
                    }

                }
            } catch
            {
                return false;
            }
        }



        /// <summary>
        /// Updates the 'Status' property of the current work item in the queue.
        /// </summary>
        /// <param name="item">The current queue work item, LMData.Queue type.</param>
        /// <param name="Status">String value to assign as the current status step.</param>
        /// <returns>Bool</returns>
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

        /// <summary>
        /// Creates a new leaderboard entry for the user if they have more minutes than their current leaderboard entry.
        /// </summary>
        /// <param name="item">The current queue work item, LMData.Queue type.</param>
        /// <param name="totalMinutes">Long value for the total minutes processed for the queue work item.</param>
        /// <returns>Bool</returns>
        private async Task<bool> SubmitToLeaderboard(Models.LMData.Queue item, long totalMinutes)
        {
            if (item.SubmitToLeaderboard == false) { return false; }

            using (var scope = _serviceProvider.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();
                
                var FoundExistingLeaderboard = await _lmdata.Leaderboard.FirstOrDefaultAsync(x => x.Username == item.Username);
                if (FoundExistingLeaderboard != null)
                {
                    if (FoundExistingLeaderboard.TotalMinutes < totalMinutes)
                    {
                        _lmdata.Leaderboard.Remove(FoundExistingLeaderboard);
                    }
                    else
                    {
                        return false;
                    }
                }

                var newLeaderboardEntry = new Leaderboard()
                {
                    Username = item.Username,
                    TotalMinutes = totalMinutes,
                    Created_On = DateTime.Now
                };

                _lmdata.Leaderboard.Add(newLeaderboardEntry);
                if (await _lmdata.SaveChangesAsync() > 0)
                {
                    return true;
                } else
                {
                    return false;
                }
                

            }
        }

        /// <summary>
        /// Clears the current work item from the queue.
        /// </summary>
        /// <param name="item">The current queue work item, LMData.Queue type.</param>
        /// <returns>Bool</returns>
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


        /// <summary>
        /// Removes any existing results from the table for the current username.
        /// </summary>
        /// <param name="Username">String value of the current work item's LastFM Username.</param>
        /// <returns>Bool</returns>
        private async Task<bool> RemoveResults(string Username)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                IQueueManager _queue = scope.ServiceProvider.GetRequiredService<IQueueManager>();

                if (await _queue.RemoveResults(Username))
                {
                    return true;
                } else
                {
                    return false;
                }

            }
        }



        private long ConvertMsToMinutesLong(long msIn)
        {
            return msIn / 60000;
        }



        /// <summary>
        /// Updates the statistics table when the current work item has been processed.
        /// </summary>
        /// <param name="totalMinutes">Long value representing the amount of minutes tallied by the current work item.</param>
        /// <returns>Bool</returns>
        private async Task<bool> UpdateStats(long totalMinutes)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                    Stats? TotalMinutes = await _lmdata.Stats.FirstOrDefaultAsync(x => x.Name == "TotalMinutes");
                    long totalMinutesLong = 0;

                    if (TotalMinutes == null)
                    {
                        TotalMinutes = new Stats()
                        {
                            Name = "TotalMinutes",
                            Data = (totalMinutes / 60000).ToString(),
                        };
                        _lmdata.Stats.Add(TotalMinutes);
                    } else
                    {
                        totalMinutesLong = long.Parse(TotalMinutes.Data);
                        totalMinutesLong = totalMinutesLong + (totalMinutes / 60000);
                        TotalMinutes.Data = totalMinutesLong.ToString();
                        _lmdata.Stats.Update(TotalMinutes);
                    }


                    Stats? TotalRuns = await _lmdata.Stats.FirstOrDefaultAsync(x => x.Name == "TotalRuns");
                    long totalRunsLong = 0;
                    if (TotalRuns == null)
                    {
                        TotalRuns = new Stats()
                        {
                            Name = "TotalRuns",
                            Data = "1",
                        };
                        _lmdata.Stats.Add(TotalRuns);
                    } else
                    {
                        totalRunsLong = long.Parse(TotalRuns.Data) + 1;
                        TotalRuns.Data = totalRunsLong.ToString();
                        _lmdata.Stats.Update(TotalRuns);
                    }

                    await _lmdata.SaveChangesAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }


        }



    }
    
    public class TrackKey
    {
        public string Name { get; set; }
        public string Artist { get; set; }

        public TrackKey(string name, string artist)
        {
            Name = name.ToLowerInvariant(); // Normalize for case-insensitive comparison
            Artist = artist.ToLowerInvariant();
        }

        public override bool Equals(object obj)
        {
            return obj is TrackKey other &&
                   Name == other.Name &&
                   Artist == other.Artist;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Artist);
        }
    }
    


    
}
