using LastMinutes.Data;

namespace LastMinutes.Services
{

    public interface IQueueManager
    {
        public Task<bool> AddUsernameToQueue(string username);
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

        public int GetLength()
        {
            return _lmdata.Queue.Count();
        }




    }
}
