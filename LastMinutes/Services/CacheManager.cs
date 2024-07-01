using LastMinutes.Data;
using LastMinutes.Models.LMData;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata.Ecma335;

namespace LastMinutes.Services
{

    public interface ICacheManager
    {
        public Task<bool> AddTrackToCache(Tracks track);

        public Task<int> GetCacheSize();

    }


    public class CacheManager : ICacheManager
    {
        private readonly ILogger<CacheManager> _logger;
        private readonly IServiceProvider _sp;

        public CacheManager(
            ILogger<CacheManager> logger,
            IServiceProvider sp) 
        {
            _logger = logger;
            _sp = sp;
        }

        public async Task<int> GetCacheSize()
        {
            using (var scope = _sp.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                List<Models.LMData.Tracks> allTracks = await _lmdata.Tracks.ToListAsync();
                return allTracks?.Count() ?? 0;
            }
        }

        public async Task<bool> AddTrackToCache(Tracks track)
        {
            // make sure result is ok
            if (string.IsNullOrEmpty(track.Name)) { return false; }
            if (string.IsNullOrEmpty(track.Artist)) { return false; }
            if (track.Runtime == 0) { return false; }

            using (var scope = _sp.CreateScope())
            {
                LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                track.Date_Added = DateTime.Now;
                track.Last_Used = DateTime.Now;

                _lmdata.Tracks.Add(track);

                if (await _lmdata.SaveChangesAsync() > 0)
                {
                    _logger.LogInformation("[Cache] Track {TrackName} by {ArtistName} was added to the cache with a runtime of {Runtime}ms", track.Name, track.Artist, track.Runtime);
                    return true;
                }
                else
                {
                    _logger.LogError("[Cache] Failed to add track to cache.");
                    return false;
                }
            }
        }


    }
}
