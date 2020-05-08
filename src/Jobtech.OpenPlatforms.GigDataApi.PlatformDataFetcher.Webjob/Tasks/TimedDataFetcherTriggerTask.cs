using System;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Tasks
{
    public class TimedDataFetcherTriggerTask: IHostedService, IDisposable
    {
        private readonly IBus _bus;
        private readonly ILogger<TimedDataFetcherTriggerTask> _logger;
        private Timer _timer;

        public TimedDataFetcherTriggerTask(IBus bus, ILogger<TimedDataFetcherTriggerTask> logger)
        {
            _bus = bus;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(EnqueueMessage, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));

            return Task.CompletedTask;
        }

        private async void EnqueueMessage(object state = null)
        {
            _logger.LogInformation($"Will enqueue {nameof(PlatformDataFetcherTriggerMessage)}");
            await _bus.SendLocal(new PlatformDataFetcherTriggerMessage());
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
