using LastMinutes.Data;
using LastMinutes.Services;
using Microsoft.AspNetCore.Mvc;

namespace LastMinutes.Controllers
{
    public class DebugController : Controller
    {

        private LMData _lmdata;
        private IQueueManager _queue;

        public DebugController(LMData lmdata, IQueueManager queue)
        {
            _lmdata = lmdata;
            _queue = queue;
        }


        /*[Route("/debug/queue/add/{username}")]
        public async Task<IActionResult> AddToQueue(string username)
        {

            if (await _queue.AddUsernameToQueue(username, ))
            {
                return Content($"Last.FM username '{username}' was successfully added to the queue. Total queue count: {_queue.GetLength()}");
            } else
            {
                return Content("Something went wrong while adding that username to the queue.");
            }

        }*/
    }
}
