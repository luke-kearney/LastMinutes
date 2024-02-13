﻿using LastMinutes.Data;
using LastMinutes.Models.LMData;
using LastMinutes.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LastMinutes.Controllers
{
    [Route("LastMinutes/api")]
    [ApiController]
    public class LastMinutesApi : ControllerBase
    {

        private readonly IConfiguration _config;
        private readonly LMData _lmdata;
        private readonly ICacheManager _cache;
        private readonly IQueueManager _queue;
        private static string LmApiKey;

        public LastMinutesApi(IConfiguration config, ICacheManager cache, LMData lmdata, IQueueManager queue)
        {
            _cache = cache;
            _config = config;
            _lmdata = lmdata;
            LmApiKey = _config.GetValue<string>("LastMinutesApiKey");
            _queue = queue;
        }



        [HttpGet]
        [Route("cache/addTrackToCache")]
        public async Task<IActionResult> AddTrackToCache(   
            [FromQuery(Name = "artist")] string artist, 
            [FromQuery(Name = "trackName")] string trackName,
            [FromQuery(Name = "runtimeMs")] int runtimeMs,
            [FromQuery(Name = "apiKey")] string apiKey)
        {
            if (string.IsNullOrEmpty(artist) ||  string.IsNullOrEmpty(trackName) || runtimeMs == 0 || string.IsNullOrEmpty(apiKey)) { return BadRequest(); }

            if (!CheckApiKey(apiKey)){
                return StatusCode(401, "Unauthorized: Missing or invalid authentication credentials.");
            }

            Tracks newTrack = new Tracks()
            {
                Name = trackName,
                Artist = artist,
                Runtime = runtimeMs
            };

            if (await _cache.AddTrackToCache(newTrack))
            {
                return Ok();
            } else
            {
                return StatusCode(500, "Internal server error occurred. Track was not added to cache!");
            }
        }


        private static bool CheckApiKey(string keyIn)
        {
            if (keyIn == LmApiKey)
            {
                return true;
            } else
            {
                return false;
            }
        }


        [HttpGet]
        [Route("queue/getLength")]
        public IActionResult GetQueueLength()
        {
            int queueLength = _queue.GetLength();
            return Content($"Total queue length: {queueLength}");
        }

        [HttpGet]
        [Route("cache/getTotalTracks")]
        public async Task<IActionResult> GetCacheTotalTracks()
        {
            int totalCache = await _cache.GetCacheSize();
            return Content($"Total cache size: {totalCache}");
        }

        [HttpGet]
        [Route("results/getAll")]
        public async Task<IActionResult> GetAllResults()
        {
            List<Models.LMData.Results> AllResults = await _queue.GetAllResults();
            string output = "All Results:<br><br>";

            foreach (Models.LMData.Results result in AllResults)
            {
                output += $"[{result.Created_On.ToString("dd/MM/yyyy")}] LastFM username: {result.Username} | Total Minutes: {_queue.ConvertMsToMinutes(result.TotalPlaytime)} minutes | Total bad scrobbles: {result.BadScrobbles.Count()} <br>";
            }

            return Content(output, "text/html");
        }


    }
}
