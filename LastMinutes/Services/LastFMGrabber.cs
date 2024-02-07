using LastMinutes.Data;

namespace LastMinutes.Services
{

    public interface ILastFMGrabber
    {

        public string Test();

    }


    public class LastFMGrabber : ILastFMGrabber
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        private string LastFMApiUrl = string.Empty;
        private string LastFMApiKey = string.Empty;

        public LastFMGrabber(
            IServiceProvider serviceProvider,
            IConfiguration config) 
        { 
            _serviceProvider = serviceProvider;
            _config = config;   

            LastFMApiUrl = config.GetValue<string>("LastFMApiUrl");
            LastFMApiKey = config.GetValue<string>("LastFMApiKey");

        }

        public string Test()
        {
            return LastFMApiUrl;
        }



    }
}
