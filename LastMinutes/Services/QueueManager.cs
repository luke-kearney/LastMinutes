using LastMinutes.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata.Ecma335;

namespace LastMinutes.Services
{

    public interface IQueueManager
    {
        public Task<bool> AddUsernameToQueue(string username);
        public Task<bool> InQueue(string username);
        public Task<bool> IsFinished(string username);

        public Task<bool> RemoveResults(string username);

        public int GetLength();

        public int GetEta();
        public string ConvertMinutesToWordsLong(int minutes); // returns the int with 'minutes' appended
        //public string ConvertMinutesToWordsShort(int minutes); // returns long time format; 1 hour and 25 minutes

        public string Busy();


    }


    public class QueueManager : IQueueManager
    {
        private readonly LMData _lmdata;


        public QueueManager(
            LMData lmdata) 
        { 
            _lmdata = lmdata;
        }


        public async Task<bool> AddUsernameToQueue(string username)
        {
            if (username == null || username == string.Empty)
            {
                return false;
            }
            LastMinutes.Models.LMData.Queue CheckExists = await _lmdata.Queue.FirstOrDefaultAsync(x => x.Username == username);
            if (CheckExists == null)
            {
                LastMinutes.Models.LMData.Queue queue = new Models.LMData.Queue()
                {
                    Username = username
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
            Models.LMData.Results results = await _lmdata.Results.FirstOrDefaultAsync();
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

        public int GetLength()
        {
            return _lmdata.Queue.Count();
        }

       /* public string ConvertMinutesToWordsShort(int minutes)
        {
            return $"{minutes} minutes";
        }*/

        public string ConvertMinutesToWordsLong(int minutes)
        {
            if (minutes < 0)
                throw new ArgumentException("Minutes cannot be negative.");

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

    }
}
