using LastMinutes.Data;
using LastMinutes.Models;
using LastMinutes.Models.LMData;
using LastMinutes.Models.MusicBrainz;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;
using System.Xml.Linq;

namespace LastMinutes.Services
{

    public interface IMusicBrainz
    {

        public Task<Scrobble> GetScrobbleData(string trackName, string artistName, int totalScrobbles);

    }


    public class MusicBrainz : IMusicBrainz
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        private string MusicBrainzApiUrl = string.Empty;
        private string MusicBrainzUserAgent = string.Empty;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(2);

        public MusicBrainz(
            IServiceProvider serviceProvider,
            IConfiguration config) 
        { 
            _serviceProvider = serviceProvider;
            _config = config;

            MusicBrainzApiUrl = config.GetValue<string>("MusicBrainzApiUrl");
            MusicBrainzUserAgent = config.GetValue<string>("MusicBrainzUserAgent");
        }


        private async Task<MusicBrainsResponseRoot> GetData(string trackName, string artistName)
        {
            HttpClient httpClient = new HttpClient();

            string encodedTrackName = Uri.EscapeDataString(trackName);
            string encodedArtistName = Uri.EscapeDataString(artistName);
            int responseMinScore = 100;
            int responseLimit = 1;

            string searchUrl = $"{MusicBrainzApiUrl}/recording?query=artist:\"{encodedArtistName}\" AND recording:\"{encodedTrackName}\"&min-score={responseMinScore}&limit={responseLimit}&fmt=json";
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(MusicBrainzUserAgent);

            HttpResponseMessage response = await httpClient.GetAsync(searchUrl);
        
            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                MusicBrainsResponseRoot mbData = JsonConvert.DeserializeObject<MusicBrainsResponseRoot>(responseBody) ?? new MusicBrainsResponseRoot() { success = false, errorMessage = "responseEmpty" };
                
                return mbData;

            } else
            {
                return new MusicBrainsResponseRoot() { success = false, errorMessage = "requestFailed" };
            }

        }


        public async Task<Scrobble> GetScrobbleData(string trackName, string artistName, int totalScrobbles)
        {
            await semaphoreSlim.WaitAsync();

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();

                    Tracks cachedTrack = await _lmdata.Tracks.FirstOrDefaultAsync(x => x.Name == trackName && x.Artist == artistName);

                    if (cachedTrack != null)
                    {
                        Console.WriteLine($"[MusicBrainz] Found cached track '{trackName}' by '{artistName}' with a runtime of '{cachedTrack.Runtime}'");

                        return new Scrobble()
                        {
                            TrackName = trackName,
                            ArtistName = artistName,
                            Count = totalScrobbles,
                            Runtime = cachedTrack.Runtime,
                            TotalRuntime = cachedTrack.Runtime * totalScrobbles
                        };

                    } else
                    {
                        MusicBrainsResponseRoot mbResponse = await GetData(trackName, artistName);
                        while (!mbResponse.success)
                        {
                            mbResponse = await GetData(trackName, artistName);
                        }
                        

                        if (mbResponse != null)
                        {
                            int TrackRuntime = mbResponse.recordings[0].length;
                            // cache track here
                            if (TrackRuntime != 0)
                            {
                                _lmdata.Tracks.Add(new Tracks()
                                {
                                    Name = trackName,
                                    Artist = artistName,
                                    Runtime = TrackRuntime,
                                    Date_Added = DateTime.Now,
                                    Last_Used = DateTime.Now
                                });
                            }

                            Console.WriteLine($"[MusicBrainz] Found a new track '{trackName}' by '{artistName}' with a runtime of '{TrackRuntime}'");
                            return new Scrobble()
                            {
                                TrackName = trackName,
                                ArtistName = artistName,
                                Count = totalScrobbles,
                                Runtime = TrackRuntime,
                                TotalRuntime = TrackRuntime * totalScrobbles
                            };
                        }
                        else
                        {
                            Console.WriteLine($"[MusicBrainz] Something went wrong when asking MusicBrainz about '{trackName}' by '{artistName}'");
                            return new Scrobble() 
                            { 
                                TrackName = trackName,
                                ArtistName = artistName,
                                Count = totalScrobbles,
                                Runtime = 0,
                                TotalRuntime = 0
                            };
                        }
                    }

                }
            }


            finally
            {
                semaphoreSlim.Release();
            }
        }

       


    }
}
