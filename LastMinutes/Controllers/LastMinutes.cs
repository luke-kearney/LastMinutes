using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using LastMinutes.Services;
using LastMinutes.Models.ViewModels;
using LastMinutes.Data;
using Microsoft.EntityFrameworkCore;

namespace LastMinutes.Controllers
{
    public class LastMinutes : Controller
    {
        private readonly IQueueManager _queue;
        private readonly ISpotifyGrabber _spotify;
        private readonly LMData _lmdata;


        public LastMinutes(IQueueManager queueManager, LMData lmdata, ISpotifyGrabber spotify)
        {
            _queue = queueManager;
            _lmdata = lmdata;
            _spotify = spotify;
        }

        public IActionResult Index()
        {
            return View("LandingPage");
        }


        [HttpPost]
        [Route("/LastMinutes/CheckMinutes")]
        public async Task<IActionResult> CheckMinutes(IFormCollection col)
        {
            string Username = col["username"].ToString();  
            if (string.IsNullOrEmpty(Username) ) { return Content("No Username Supplied!"); }

            bool IsInQueue = await _queue.InQueue(Username);
            bool IsFinished = await _queue.IsFinished(Username);

            if (IsFinished)
            {
                Models.LMData.Results Result = await _lmdata.Results.FirstOrDefaultAsync(x => x.Username == Username);
                if (Result == null)
                {
                    return Content("Something went very wrong...");
                }

                ResultsViewModel vm = new ResultsViewModel()
                {
                    Username = Username,
                    TotalMinutes = _spotify.ConvertMsToMinutes(Result.TotalPlaytime),
                    TotalMs = Result.TotalPlaytime
                };

                return View("ResultsPage", vm);
            }

            if (IsInQueue)
            {
                return View("PendingPage");
            }

            
            if (await _queue.AddUsernameToQueue(Username))
            {
                return View("AddedPage");
            } else
            {
                return Content("Something went wrong, please try again.");
            }

           
        }


        


    }
}
