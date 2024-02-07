using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LastMinutes.Services
{
    public class QueueController : BackgroundService
    {

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
            Console.WriteLine("[Queue] Checking Database for new requests...");
            await Task.Delay(TimeSpan.FromSeconds(15));
        }

    }
}
