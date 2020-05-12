using System;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Messages;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core
{
    public abstract class DataFetcherBase<TConnectionInfo> : IDataFetcher<TConnectionInfo>
        where TConnectionInfo : IPlatformConnectionInfo
    {
        private readonly IBus _bus;
        protected readonly ILogger<DataFetcherBase<TConnectionInfo>> Logger;

        protected DataFetcherBase(IBus bus, ILogger<DataFetcherBase<TConnectionInfo>> logger)
        {
            _bus = bus;
            Logger = logger;
        }

        public Task<TConnectionInfo> StartDataFetch(string userId, string platformId, TConnectionInfo connectionInfo,
            PlatformConnection platformConnection, DataSyncLog syncLog, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        protected async Task CompleteDataFetch(string userId, string platformId, PlatformDataFetchResult fetchResult, string syncLogId,
            CancellationToken cancellationToken = default)
        {
            await _bus.SendLocal(new DataFetchCompleteMessage(userId, platformId, fetchResult, syncLogId));
        }

        protected async Task CompleteDataFetchWithConnectionRemoved(string userId, string platformId, PlatformConnectionDeleteReason deleteReason,
            string syncLogId, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Will send PlatformConnectionRemovedMessage with delete reason {DeleteReason}.", deleteReason);
            var message = new PlatformConnectionRemovedMessage(userId, platformId, deleteReason, syncLogId);
            await _bus.SendLocal(message);
        }
    }
}
