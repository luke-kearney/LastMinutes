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
        private readonly ICacheManager _cache;
        private static string LmApiKey;

        public LastMinutesApi(IConfiguration config, ICacheManager cache)
        {
            _cache = cache;
            _config = config;
            LmApiKey = _config.GetValue<string>("LastMinutesApiKey");
        }



        [HttpGet]
        [Route("AddTrackToCache")]
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

    }
}
