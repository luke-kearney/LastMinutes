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

        public int GetLength(); 

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

        public int GetLength()
        {
            return _lmdata.Queue.Count();
        }




    }
}
