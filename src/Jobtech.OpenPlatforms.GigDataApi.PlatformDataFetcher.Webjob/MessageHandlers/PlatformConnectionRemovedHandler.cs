using System;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Messages;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Retry.Simple;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class PlatformConnectionRemovedHandler : IHandleMessages<PlatformConnectionRemovedMessage>, IHandleMessages<IFailed<PlatformConnectionRemovedMessage>>
    {
        private readonly IDocumentStore _documentStore;
        private readonly ILogger<PlatformConnectionRemovedHandler> _logger;
        private readonly IPlatformDataManager _platformDataManager;
        private readonly IAppNotificationManager _appNotificationManager;
        private readonly IBus _bus;

        public PlatformConnectionRemovedHandler(IPlatformDataManager platformDataManager, IAppNotificationManager appNotificationManager, IDocumentStore documentStore, IBus bus, 
            ILogger<PlatformConnectionRemovedHandler> logger)
        {
            _platformDataManager = platformDataManager;
            _appNotificationManager = appNotificationManager;
            _documentStore = documentStore;
            _bus = bus;
            _logger = logger;
        }

        public async Task Handle(PlatformConnectionRemovedMessage message)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                //remove connection
                var user = await session.LoadAsync<User>(message.UserId);
                var index = 0;
                var indexToRemove = -1;
                PlatformConnection platformConnectionToRemove = null;

                foreach (var userPlatformConnection in user.PlatformConnections)
                {
                    if (userPlatformConnection.PlatformId == message.PlatformId)
                    {
                        indexToRemove = index;
                        platformConnectionToRemove = userPlatformConnection;
                    }

                    index++;
                }

                if (indexToRemove == -1 || platformConnectionToRemove == null)
                {
                    _logger.LogWarning($"Platform connection with platform id {message.PlatformId} was not found for user with id {message.UserId}");
                    return;
                }

                user.PlatformConnections.RemoveAt(indexToRemove);
                await session.SaveChangesAsync();

                await _appNotificationManager.NotifyPlatformConnectionRemoved(message.UserId,
                    platformConnectionToRemove.ConnectionInfo.NotificationInfos.Select(ni => ni.AppId).ToList(),
                    platformConnectionToRemove.PlatformId, platformConnectionToRemove.ExternalPlatformId, platformConnectionToRemove.PlatformName, session);
            }
            
        }

        public async Task Handle(IFailed<PlatformConnectionRemovedMessage> message)
        {
            await _bus.DeferLocal(TimeSpan.FromSeconds(60), message.Message);
        }
    }
}
