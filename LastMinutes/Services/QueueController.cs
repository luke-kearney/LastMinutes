using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LastMinutes.Services
{
    public class QueueController : BackgroundService
    {

        private readonly ILastFMGrabber _lastfm;

        public QueueController(ILastFMGrabber lastfm)
        {
            _lastfm = lastfm;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                await CheckDatabase();

            }
        }


        private async Task CheckDatabase()
        {
            Console.WriteLine($"[Queue] LastFM API URL is {_lastfm.Test()}");
            await Task.Delay(TimeSpan.FromSeconds(15));
        }

    }
}
