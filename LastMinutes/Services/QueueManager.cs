using LastMinutes.Data;
using LastMinutes.Models.LMData;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Diagnostics.Contracts;
using System.Reflection.Metadata.Ecma335;

namespace LastMinutes.Services
{

    public interface IQueueManager
    {
        public Task<bool> AddUsernameToQueue(string username, int mode);
        public Task<bool> InQueue(string username);
        public Task<bool> IsFinished(string username);
        public Task<bool> RemoveResults(string username);

        public int GetLength();

        public Task<List<Models.LMData.Results>> GetAllResults();
        public int GetEta();
        public string ConvertMinutesToWordsLong(int minutes); // returns the int with 'minutes' appended
        //public string ConvertMinutesToWordsShort(int minutes); // returns long time format; 1 hour and 25 minutes

        public string Busy();

        public Task<bool> CanRefresh(string username);

        public Task<int> Cooldown(string username);

        public string GetBadScrobbleText(int count);

        public bool IsSpecialAccount(string username);

        public string ConvertMsToMinutes(long durationMs);
    }


    public class QueueManager : IQueueManager
    {
        private readonly LMData _lmdata;
        private readonly IConfiguration _config;
        private readonly string[] LastFmFriends;
        private int CooldownHours = 1;

        public QueueManager(
            LMData lmdata,
            IConfiguration config) 
        { 
            _lmdata = lmdata;
            _config = config;

            string lastFmFriendsRaw = _config.GetValue<string>("SpecialAccounts");
            LastFmFriends = JsonConvert.DeserializeObject<string[]>(lastFmFriendsRaw) ?? new string[] { "MRDAWGZA", "KIARA_DONUT" }; 
        }


        public async Task<bool> AddUsernameToQueue(string username, int mode)
        {
            if (username == null || username == string.Empty)
            {
                return false;
            }

            /*
             // This code has been removed because it was used as a rate limiting feature and is no longer needed. 
             
            int[] PremiumModes = { 1, 4, 7 };

            if (PremiumModes.Contains(mode))
            { // user selected premium, check if they are special
                if (!LastFmFriends.Contains(username.ToUpper()))
                {
                    mode = 3;
                }
            } 

            */

            if (mode == 0)
            {
                mode = 3;
            }

            LastMinutes.Models.LMData.Queue? CheckExists = await _lmdata.Queue.FirstOrDefaultAsync(x => x.Username == username);
            if (CheckExists == null)
            {
                LastMinutes.Models.LMData.Queue queue = new Models.LMData.Queue()
                {
                    Username = username,
                    Mode = mode,
                    Status = "Currently waiting in queue..."
                };

                _lmdata.Queue.Add(queue);

                if (await _lmdata.SaveChangesAsync() > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            } else
            {
                return true;
            }

            
        }

        public async Task<bool> InQueue(string username)
        {
            if (username == null || username == string.Empty)
            {
                return false;
            }

            Models.LMData.Queue QueueItem = await _lmdata.Queue.FirstOrDefaultAsync(x => x.Username == username);

            if (QueueItem == null)
            {
                return false;
            } else
            {
                return true;
            }
        }

        public async Task<bool> IsFinished(string username)
        {
            if (username == null || username == string.Empty)
            {
                return false;
            }

            Models.LMData.Results result = await _lmdata.Results.FirstOrDefaultAsync(x => x.Username == username);

            if (result == null)
            {
                return false;
            } else
            {
                return true;
            }

        }

        public async Task<bool> RemoveResults(string username)
        {
            Models.LMData.Results results = await _lmdata.Results.FirstOrDefaultAsync(x => x.Username.ToUpper() ==  username.ToUpper());
            if (results == null) { return false;}

            _lmdata.Results.Remove(results);

            if (await _lmdata.SaveChangesAsync() > 0)
            {
                return true;
            } else
            {
                return false;
            }

        }

        public async Task<string> GetQueueItemStatus(string username)
        {
            if (string.IsNullOrEmpty(username)) { return "Error"; }

            Models.LMData.Queue queue = await _lmdata.Queue.FirstOrDefaultAsync(x => x.Username == username);

            if (queue == null)
            {
                return "Done";
            } else
            {
                return queue.Status;
            }

        }

        public async Task<List<Models.LMData.Results>> GetAllResults()
        {
            List<Models.LMData.Results> allResults = await _lmdata.Results.ToListAsync();
            return allResults ?? new List<Models.LMData.Results>();
        }

        public string ConvertMsToMinutes(long durationMs)
        {
            // Convert milliseconds to minutes
            long totalMinutes = durationMs / (1000 * 60);

            // Return the result as a string
            return totalMinutes.ToString();
        }

        public int GetLength()
        {
            return _lmdata.Queue.Count();
        }

        public string ConvertMinutesToWordsLong(int minutes)
        {
            if (minutes < 0)
                minutes = 0;

            if (minutes == 0)
                return "0 minutes";

            int hours = minutes / 60;
            int remainingMinutes = minutes % 60;

            string hourString = hours == 1 ? "hour" : "hours";
            string minuteString = remainingMinutes == 1 ? "minute" : "minutes";

            if (hours == 0)
            {
                return $"{remainingMinutes} {minuteString}";
            }
            else if (remainingMinutes == 0)
            {
                return $"{hours} {hourString}";
            }
            else
            {
                return $"{hours} {hourString} and {remainingMinutes} {minuteString}";
            }
        }

        public int GetEta()
        {
            int QueueLength = GetLength();
            if (QueueLength == 0)
            {
                QueueLength++;
            }

            int AverageMinutes = 15;

            return QueueLength * AverageMinutes;
        }

        public string Busy()
        {
            int queueLength = GetLength(); 

            if (queueLength < 2)
            {
                return "not busy";
            }
            else if (queueLength >= 2 && queueLength < 5)
            {
                return "slightly busy";
            }
            else if (queueLength >= 5 && queueLength < 10)
            {
                return "busy";
            }
            else if (queueLength >= 10 && queueLength < 20)
            {
                return "quite busy";
            }
            else
            {
                return "very busy";
            }
        }

        public async Task<bool> CanRefresh(string username)
        {
            // How long should the public cooldown be?
            int HoursFrom = CooldownHours;

            // Format the string to upcase
            string lastFmUsername = username.ToUpper();

            // Get the created_on
            LastMinutes.Models.LMData.Results Result = await _lmdata.Results.FirstOrDefaultAsync(x => x.Username.ToUpper() == lastFmUsername );
            if (Result == null)
            {
                return false;
            }

            // Calculate the difference
            TimeSpan timeSinceCreated = DateTime.Now - Result.Created_On;

            // Check the difference from the set cooldown time
            if ((int)timeSinceCreated.TotalHours >  HoursFrom)
            {
                // return true if they can refresh their minutes
                return true;
            } else
            {
                // check if the username is in my friends list
                if (LastFmFriends.Contains(lastFmUsername))
                {
                    // I know this person, they can skip the wait
                    return true;
                }
                return false;
            }
        }

        public async Task<int> Cooldown(string username)
        {
            string lastFmUsername = username.ToUpper();
            LastMinutes.Models.LMData.Results Result = await _lmdata.Results.FirstOrDefaultAsync(x => x.Username.ToUpper() == lastFmUsername);
            if (Result == null)
            {
                return 0;
            }

            TimeSpan TimePassed = DateTime.Now - Result.Created_On;
            TimeSpan RemainingTime = TimeSpan.FromHours(CooldownHours) - TimePassed;
            int hoursLeft = (int)Math.Ceiling(RemainingTime.TotalMinutes);

            return hoursLeft;
        }

        public string GetBadScrobbleText(int count)
        {
            if (count == 0)
            {
                return "no bad scrobbles :)";
            }else if (count < 2)
            {
                return "a couple of bad scrobbles :/";
            }
            else if (count < 5)
            {
                return "a handful of bad scrobbles :/";
            }
            else if (count < 10)
            {
                return "a lot of bad scrobbles :/";
            }
            else if (count < 15)
            {
                return "quite a lot of bad scrobbles :(";
            }
            else
            {
                return "a ton of bad scrobbles :(";
            }
        }

        public bool IsSpecialAccount(string username)
        {
            if (LastFmFriends.Contains(username.ToUpper()))
            {
                return true;
            } else
            {
                return false;
            }
        }
    }
}
