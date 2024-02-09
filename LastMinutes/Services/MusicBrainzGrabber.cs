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

        public Task<Scrobble> GetScrobbleData(Scrobble ScrobbleIn);

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

                await Task.Delay(TimeSpan.FromSeconds(1));
                return mbData;

            } else
            {
                return new MusicBrainsResponseRoot() { success = false, errorMessage = response.StatusCode.ToString() };
            }

        }


        public async Task<Scrobble> GetScrobbleData(Scrobble ScrobbleIn)
        {
            await semaphoreSlim.WaitAsync();

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    LMData _lmdata = scope.ServiceProvider.GetRequiredService<LMData>();


                    MusicBrainsResponseRoot mbResponse = await GetData(ScrobbleIn.TrackName, ScrobbleIn.ArtistName);
                    while (!mbResponse.success)
                    {
                        Console.WriteLine($"[MusicBrainz] Error: {mbResponse.errorMessage}");
                        mbResponse = await GetData(ScrobbleIn.TrackName, ScrobbleIn.ArtistName);
                    }

                    

                    if (mbResponse != null)
                    {
                        int TrackRuntime = 0;
                        if (mbResponse.recordings != null)
                        {
                            if (mbResponse.recordings.Count != 0)
                            {
                                TrackRuntime = mbResponse.recordings[0].length;
                            }
                        }
                        // cache track here
                        if (TrackRuntime != 0)
                        {
                            _lmdata.Tracks.Add(new Tracks()
                            {
                                Name = ScrobbleIn.TrackName,
                                Artist = ScrobbleIn.ArtistName,
                                Runtime = TrackRuntime,
                                Date_Added = DateTime.Now,
                                Last_Used = DateTime.Now
                            });
                            if (await _lmdata.SaveChangesAsync() > 0)
                            {
                                Console.WriteLine($"[Cache] New item added: '{ScrobbleIn.TrackName}' by '{ScrobbleIn.ArtistName}', runtime '{TrackRuntime}'");
                            }
                        }

                        Console.WriteLine($"[MusicBrainz] Found a new track '{ScrobbleIn.TrackName}' by '{ScrobbleIn.ArtistName}' with a runtime of '{TrackRuntime}'");
                        ScrobbleIn.Runtime = TrackRuntime;
                        ScrobbleIn.TotalRuntime = TrackRuntime * ScrobbleIn.Count;
                        return ScrobbleIn;
                    }
                    else
                    {
                        Console.WriteLine($"[MusicBrainz] Something went wrong when asking MusicBrainz about '{ScrobbleIn.TrackName}' by '{ScrobbleIn.ArtistName}'");
                        return ScrobbleIn;

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
