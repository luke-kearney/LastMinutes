using LastMinutes.Data;
using LastMinutes.Models.LMData;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata.Ecma335;

namespace LastMinutes.Services
{

    public interface ICacheManager
    {
        public Task<bool> AddTrackToCache(Tracks track);
    }


    public class CacheManager : ICacheManager
    {
        private readonly LMData _lmdata;

        public CacheManager(
            LMData lmdata) 
        { 
            _lmdata = lmdata;
        }

        public async Task<bool> AddTrackToCache(Tracks track)
        {

            // make sure result is ok
            if (string.IsNullOrEmpty(track.Name)) { return false; }
            if (string.IsNullOrEmpty(track.Artist)) { return false; }
            if (track.Runtime == 0) { return false; }

            track.Date_Added = DateTime.Now;
            track.Last_Used = DateTime.Now;

            _lmdata.Tracks.Add(track);

            if (await _lmdata.SaveChangesAsync() > 0)
            {
                return true;
            } else
            {
                return false;
            }

        }

    }
}
