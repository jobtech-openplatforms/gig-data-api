using System;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Messages;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Retry.Simple;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class DataFetchCompleteHandler : IHandleMessages<DataFetchCompleteMessage>, IHandleMessages<IFailed<DataFetchCompleteMessage>>
    {
        private readonly IDocumentStore _documentStore;
        private readonly IPlatformDataManager _platformDataManager;
        private readonly IAppNotificationManager _appNotificationManager;
        private readonly IBus _bus;

        public DataFetchCompleteHandler(IPlatformDataManager platformDataManager, IAppNotificationManager appNotificationManager, IDocumentStore documentStore, IBus bus)
        {
            _platformDataManager = platformDataManager;
            _appNotificationManager = appNotificationManager;
            _documentStore = documentStore;
            _bus = bus;
        }

        public async Task Handle(DataFetchCompleteMessage message)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var (platformDataId, platformConnection) = await HandleFetchDataResult(message.UserId, message.PlatformId, message.Result, session);

                await session.SaveChangesAsync();

                await _appNotificationManager.NotifyPlatformConnectionDataUpdate(message.UserId,
                    platformConnection.ConnectionInfo.NotificationInfos.Select(ni => ni.AppId).ToList(),
                    platformConnection.PlatformId, platformConnection.ExternalPlatformId, platformConnection.PlatformName, platformDataId, session);
            }
            
        }

        public async Task Handle(IFailed<DataFetchCompleteMessage> message)
        {
            await _bus.DeferLocal(TimeSpan.FromSeconds(60), message.Message);
        }

        private async Task<(string PlatformDataId, PlatformConnection platformConnection)> HandleFetchDataResult(string userId, string platformId, 
            PlatformDataFetchResult result, IAsyncDocumentSession session)
        {
            var user = await session.LoadAsync<User>(userId);

            var platformData = await _platformDataManager.AddPlatformData(userId, platformId, result.NumberOfGigs, result.PeriodStart,
                result.PeriodEnd, result.Ratings, result.AverageRating, result.Reviews, result.Achievements, result.RawData, session);

            var platformConnection = user.PlatformConnections.Single(pc => pc.PlatformId == platformId);
            platformConnection.MarkAsDataFetchSuccessful();

            return (platformData.Id, platformConnection);
        }
    }
}
