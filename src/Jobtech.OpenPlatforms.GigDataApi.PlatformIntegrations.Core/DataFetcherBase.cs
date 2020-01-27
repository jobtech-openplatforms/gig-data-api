using System;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Messages;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core
{
    public abstract class DataFetcherBase<TConnectionInfo> : IDataFetcher<TConnectionInfo>
        where TConnectionInfo : IPlatformConnectionInfo
    {
        private readonly IBus _bus;

        protected DataFetcherBase(IBus bus)
        {
            _bus = bus;
        }

        public Task<TConnectionInfo> StartDataFetch(string userId, string platformId, TConnectionInfo connectionInfo, PlatformConnection platformConnection)
        {
            throw new NotImplementedException();
        }

        protected async Task CompleteDataFetch(string userId, string platformId, PlatformDataFetchResult fetchResult)
        {
            await _bus.SendLocal(new DataFetchCompleteMessage(userId, platformId, fetchResult));
        }

        protected async Task CompleteDataFetchWithConnectionRemoved(string userId, string platformId)
        {
            await _bus.SendLocal(new PlatformConnectionRemovedMessage(userId, platformId));
        }
    }
}
