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
using LastMinutes.Models.LMData;
using LastMinutes.Models;

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

        [Route("faq")]
        public async Task<IActionResult> Faq()
        {
            List<Tracks> AllCached = await _lmdata.Tracks.ToListAsync();
            int TotalCached = AllCached.Count;
            FaqViewModel vm = new FaqViewModel()
            {
                TotalCachedTracks = TotalCached,
            };
            
            return View("FaqPage", vm);
        }

        [Route("/results/{Username}")]
        public async Task<IActionResult> ResultsIndex(string Username)
        {
            if (string.IsNullOrEmpty(Username)) { return Content("Error, no username supplied!"); }

            bool IsInQueue = await _queue.InQueue(Username);
            bool IsFinished = await _queue.IsFinished(Username);

            if (IsInQueue)
            {
                PendingViewModel spvm = new PendingViewModel()
                {
                    Username = Username,
                    Eta = _queue.GetEta(),
                    EtaWords = _queue.ConvertMinutesToWordsLong(_queue.GetEta()),
                    ServerStatus = _queue.Busy()
                };
                return View("PendingPage", spvm);
            }

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
                    TotalMs = Result.TotalPlaytime,

                    TopScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.AllScrobbles) ?? new List<Scrobble>(),
                    BadScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.BadScrobbles) ?? new List<Scrobble>()
                };

                // Check if it's been long enough to requeue 
                vm.CanRefresh = await _queue.CanRefresh(Username);
                vm.Cooldown = await _queue.Cooldown(Username);
                vm.CooldownText = _queue.ConvertMinutesToWordsLong(vm.Cooldown);

                return View("Results/Index", vm);
            }


            // Does not exist, go to creation page
            return RedirectToAction("Index", "LastMinutes");
        }


        [Route("/results/{Username}/BadScrobbles")]
        public async Task<IActionResult> BadScrobblesIndex(string Username)
        {
            if (string.IsNullOrEmpty(Username)) { return Content("Error, no username supplied!"); }

            bool IsInQueue = await _queue.InQueue(Username);
            bool IsFinished = await _queue.IsFinished(Username);

            if (IsInQueue)
            {
                PendingViewModel spvm = new PendingViewModel()
                {
                    Username = Username,
                    Eta = _queue.GetEta(),
                    EtaWords = _queue.ConvertMinutesToWordsLong(_queue.GetEta()),
                    ServerStatus = _queue.Busy()
                };
                return View("PendingPage", spvm);
            }

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
                    TotalMs = Result.TotalPlaytime,

                    TopScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.AllScrobbles) ?? new List<Scrobble>(),
                    BadScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.BadScrobbles) ?? new List<Scrobble>()
                };

                // Check if it's been long enough to requeue 
                vm.CanRefresh = await _queue.CanRefresh(Username);

                // Get bad scrobble text
                vm.BadScrobbleText = _queue.GetBadScrobbleText(vm.BadScrobbles.Count());

                return View("Results/BadScrobbles", vm);
            }


            // Does not exist, go to creation page
            return RedirectToAction("Index", "LastMinutes");
        }

        [Route("results/{Username}/TopScrobbles")]
        public async Task<IActionResult> TopScrobblesIndex(string Username)
        {
            if (string.IsNullOrEmpty(Username)) { return Content("Error, no username supplied!"); }

            bool IsInQueue = await _queue.InQueue(Username);
            bool IsFinished = await _queue.IsFinished(Username);

            if (IsInQueue)
            {
                PendingViewModel spvm = new PendingViewModel()
                {
                    Username = Username,
                    Eta = _queue.GetEta(),
                    EtaWords = _queue.ConvertMinutesToWordsLong(_queue.GetEta()),
                    ServerStatus = _queue.Busy()
                };
                return View("PendingPage", spvm);
            }

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
                    TotalMs = Result.TotalPlaytime,

                    TopScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.AllScrobbles) ?? new List<Scrobble>(),
                    BadScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.BadScrobbles) ?? new List<Scrobble>()
                };

                // Check if it's been long enough to requeue 
                //vm.CanRefresh = await _queue.CanRefresh(Username);

                // Get bad scrobble text
                //vm.BadScrobbleText = _queue.GetBadScrobbleText(vm.BadScrobbles.Count());

                return View("Results/TopScrobbles", vm);
            }


            // Does not exist, go to creation page
            return RedirectToAction("Index", "LastMinutes");
        }


        [Route("/refresh/{Username}")]
        public async Task<IActionResult> Refresh(string Username)
        {
            if (string.IsNullOrEmpty(Username)) { return Content("Error, no username supplied!"); }

            bool IsInQueue = await _queue.InQueue(Username);
            bool IsFinished = await _queue.IsFinished(Username);

            if (IsFinished)
            {
                // purge results and requeu 
                if (await _queue.RemoveResults(Username))
                {
                    // results removed, now requeue
                    if (await _queue.AddUsernameToQueue(Username))
                    {
                        return RedirectToAction("ResultsIndex", "LastMinutes", new { Username = Username });
                    }
                }

                return Content("Something went wrong while removing or requeuing your username. Please try again or contact us.");
            }

            if (IsInQueue)
            {
                return RedirectToAction("ResultsIndex", "LastMinutes", new { Username = Username });
            }


            return RedirectToAction("Index", "LastMinutes");
            

        }



        [HttpPost]
        [Route("/LastMinutes/CheckMinutes")]
        public async Task<IActionResult> CheckMinutes(IFormCollection col)
        {
            string Username = col["username"].ToString();
            if (string.IsNullOrEmpty(Username)) { return Content("No Username Supplied!"); }

            // Get the mode required
            string ModeIn = col["Mode"].ToString() ?? "3";
            int Mode = 0;
            switch (ModeIn)
            {
                case "1": Mode = 1; break; // Over All Time
                case "2": Mode = 2; break; // Over The Last Week
                case "3": Mode = 3; break; // Over The Last Month
                case "4": Mode = 4; break; // Over The Last Year
                case "5": Mode = 5; break; // This Week
                case "6": Mode = 6; break; // This Month
                case "7": Mode = 7; break; // This Year
            }

            bool IsInQueue = await _queue.InQueue(Username);
            bool IsFinished = await _queue.IsFinished(Username);

            if (IsFinished)
            {
                return RedirectToAction("ResultsIndex", "LastMinutes", new { Username = Username });
            }

            PendingViewModel spvm = new PendingViewModel()
            {
                Username = Username,
                Eta = _queue.GetEta(),
                EtaWords = _queue.ConvertMinutesToWordsLong(_queue.GetEta()),
                ServerStatus = _queue.Busy()
            };


            if (IsInQueue)
            {
                return RedirectToAction("ResultsIndex", "LastMinutes", new { Username = Username }); 
            }


            if (await _queue.AddUsernameToQueue(Username, Mode))
            {
                return RedirectToAction("ResultsIndex", "LastMinutes", new { Username = Username });
            } 
            
            
            return Content("Something went wrong, please try again.");
            

           
        }


        


    }
}
