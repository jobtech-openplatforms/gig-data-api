using System;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Messages;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Retry.Simple;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class FetchDataForPlatformConnectionHandler: IHandleMessages<FetchDataForPlatformConnectionMessage>, IHandleMessages<IFailed<FetchDataForPlatformConnectionMessage>>
    {
        private readonly IFreelancerDataFetcher _freelancerDataFetcher;
        private readonly IGigPlatformDataFetcher _gigPlatformDataFetcher;
        private readonly ILogger<FetchDataForPlatformConnectionHandler> _logger;
        private readonly IDocumentStore _documentStore;
        private readonly IBus _bus;

        public FetchDataForPlatformConnectionHandler(IFreelancerDataFetcher freelancerDataFetcher,
            IGigPlatformDataFetcher gigPlatformDataFetcher,
            IDocumentStore documentStore, IBus bus,
            ILogger<FetchDataForPlatformConnectionHandler> logger)
        {
            _freelancerDataFetcher = freelancerDataFetcher;
            _gigPlatformDataFetcher = gigPlatformDataFetcher;
            _bus = bus;
            _documentStore = documentStore;
            _logger = logger;
        }

        public async Task Handle(FetchDataForPlatformConnectionMessage message)
        {
            _logger.LogInformation($"Will start data fetch. User: {message.UserId}, PlatformId: {message.PlatformId}");

            using (var session = _documentStore.OpenAsyncSession())
            {
                var user = await session.LoadAsync<User>(message.UserId);
                var platformConnection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == message.PlatformId);
                if (platformConnection == null)
                {
                    throw new PlatformConnectionDoesNotExistException($"Platform connection for platform with id {message.PlatformId} does not exist for user {message.UserId}");
                }

                var connectionInfo = platformConnection.ConnectionInfo;

                switch (message.PlatformIntegrationType)
                {
                    case PlatformIntegrationType.AirbnbIntegration:
                    case PlatformIntegrationType.UpworkIntegration:
                    case PlatformIntegrationType.Manual:
                        _logger.LogError($"Data fetcher not implemented. PlatformId: {message.PlatformId}");
                        throw new NotImplementedException();
                    case PlatformIntegrationType.GigDataPlatformIntegration:
                        connectionInfo = await _gigPlatformDataFetcher.StartDataFetch(message.UserId,
                            message.PlatformId, (OAuthOrEmailPlatformConnectionInfo) connectionInfo,
                            platformConnection);
                        break;
                    case PlatformIntegrationType.FreelancerIntegration:
                        connectionInfo = await _freelancerDataFetcher.StartDataFetch(message.UserId, message.PlatformId,
                            (OAuthPlatformConnectionInfo) connectionInfo, platformConnection);
                        break;
                }

                if (connectionInfo.IsDeleted)
                {
                    //we no longer have a connection to the platform for the given user.
                    return;
                }

                platformConnection.ConnectionInfo = connectionInfo;
                await session.SaveChangesAsync();
            }
        }

        public async Task Handle(IFailed<FetchDataForPlatformConnectionMessage> message)
        {
            _logger.LogInformation($"Handling failed {nameof(FetchDataForPlatformConnectionMessage)}. Will defer 60 seconds");
            await _bus.DeferLocal(TimeSpan.FromSeconds(60), message.Message);
        }
    }
}
