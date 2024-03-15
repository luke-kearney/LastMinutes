﻿using Microsoft.AspNetCore.Mvc;
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
using LastMinutes.ActionFilters;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;

namespace LastMinutes.Controllers
{

    [ServiceFilter(typeof(VersionAppending))]
    public class LastMinutes : Controller
    {
        private readonly IQueueManager _queue;
        private readonly ISpotifyGrabber _spotify;
        private readonly LMData _lmdata;
        private readonly IConfiguration _config;


        public LastMinutes(IQueueManager queueManager, LMData lmdata, ISpotifyGrabber spotify, IConfiguration config)
        {
            _queue = queueManager;
            _lmdata = lmdata;
            _spotify = spotify;
            _config = config;
        }

        #region Standard Logic

        public IActionResult Index()
        {
            ViewBag.SignedIn = false;
            ViewBag.Username = "";

            string LoginCookie = Request.Cookies["Username"];
            if (LoginCookie != null )
            {
                ViewBag.SignedIn = true;
                ViewBag.Username = LoginCookie;
            }
            LandingPageViewModel lpvm = new LandingPageViewModel()
            {
                ShowMessage = _config.GetValue<bool>("LandingShowMessage"),
                Message = _config.GetValue<string>("LandingMessage")
            };

            return View("LandingPage", lpvm);
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

        [Route("/debug/app-status")]
        public async Task<IActionResult> AppStatus()
        {
            AppStatusViewModel asvm = new AppStatusViewModel()
            {
                QueueLength = _queue.GetLength(),
                ResultsAmount = _lmdata.Results.Count(),
                TrackCache = await _lmdata.Tracks.CountAsync(),
                SpotifyResponseTime = 0,
                DeezerResponseTime = 0,
                LastFmResponseTime = 0,
            };

            return View("AppStatus", asvm);
        }

        [Route("release-notes")]
        public IActionResult ReleaseNotes()
        {
            return View("ReleaseNotes");
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
                    ServerStatus = _queue.Busy(),
                    ShowMessage = _config.GetValue<bool>("PendingShowMessage"),
                    Message = _config.GetValue<string>("PendingMessage")
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
                    BadScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.BadScrobbles) ?? new List<Scrobble>(),

                    TimeFrame = Result.TimeFrame,
                    FromWhen = Result.FromWhen,
                    ToWhen = Result.ToWhen
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
                Models.LMData.Results? Result = await _lmdata.Results.FirstOrDefaultAsync(x => x.Username == Username);
                if (Result == null)
                {
                    return Content("Something went very wrong...");
                }

                List<Scrobble> BadScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.BadScrobbles) ?? new List<Scrobble>();

                ResultsViewModel vm = new ResultsViewModel()
                {
                    Username = Username,
                    TotalMinutes = _spotify.ConvertMsToMinutes(Result.TotalPlaytime),
                    TotalMs = Result.TotalPlaytime,

                    TopScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.AllScrobbles) ?? new List<Scrobble>(),
                    BadScrobbles = BadScrobbles.Take(100).ToList(),

                    TimeFrame = Result.TimeFrame,
                    FromWhen = Result.FromWhen,
                    ToWhen = Result.ToWhen
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
                    BadScrobbles = JsonConvert.DeserializeObject<List<Scrobble>>(Result.BadScrobbles) ?? new List<Scrobble>(),

                    TimeFrame = Result.TimeFrame,
                    FromWhen = Result.FromWhen,
                    ToWhen = Result.ToWhen
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
                    /*if (await _queue.AddUsernameToQueue(Username))
                    {
                        return RedirectToAction("ResultsIndex", "LastMinutes", new { Username = Username });
                    }*/
                    return RedirectToAction("ResultsIndex", "LastMinutes", new {Username = Username});
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
        [Route("/go/checkminutes")]
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
                ServerStatus = _queue.Busy(),
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


        [Route("go/signin/failure")]
        public IActionResult SignInFailure()
        {
            return View("SignInInfo");
        }


        [Route("/SignIn")]
        public IActionResult SignInIndex()
        {
            return View("SignIn");
        }

        [HttpPost]
        [Route("/go/signin")]
        public async Task<IActionResult> SignIn(IFormCollection col)
        {
            string Username = col["username"].ToString();
            if (string.IsNullOrEmpty(Username)) { return Content("No Username Supplied!"); }

            if (_queue.IsSpecialAccount(Username))
            {
                CookieOptions option = new CookieOptions();
                option.Expires = DateTime.Now.AddDays(31);

                Response.Cookies.Append("Username", Username, option);

                return RedirectToAction("Index", "LastMinutes");
            } else
            {
                return RedirectToAction("SignInFailure", "LastMinutes");
            }

        }

        [Route("go/signout")]
        public async Task<IActionResult> SignOut()
        {
            Response.Cookies.Delete("Username");
            return RedirectToAction("SignInIndex", "LastMinutes");

        }

        #endregion

        #region Errors

        [Route("Error/400")]
        public IActionResult ErrorBadRequest()
        {
            Response.StatusCode = 400;
            return View("Errors/400");
        }

        [Route("Error/404")]
        public IActionResult ErrorNotFound()
        {
            Response.StatusCode = 404;
            return View("Errors/404");
        }

        [Route("Error/500")]
        public IActionResult ErrorInternalServerError()
        {
            Response.StatusCode = 500;
            return View("Errors/500");
        }

        [Route("Error/429")]
        public IActionResult ErrorTooManyRequests()
        {
            Response.StatusCode = 429;
            return View("Errors/429");
        }

        [Route("Error/418")]
        public IActionResult ErrorImATeapot()
        {
            Response.StatusCode = 418;
            return View("Errors/418");
        }

        #endregion


        #region Methods 

        private async Task<long> GetResponseTimeAsync(string url)
        {
            try
            {
                var ping = new System.Net.NetworkInformation.Ping();
                var result = ping.Send(url);
                if (result.Status != System.Net.NetworkInformation.IPStatus.Success)
                {
                    return -1;
                }
                return result.RoundtripTime;
            } catch
            {
                return -1;
            }

        }

        #endregion

    }
}
