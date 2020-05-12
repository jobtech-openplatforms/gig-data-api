using System;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Common.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Extensions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Retry.Simple;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class FetchDataForPlatformConnectionHandler: IHandleMessages<FetchDataForPlatformConnectionMessage>, IHandleMessages<IFailed<FetchDataForPlatformConnectionMessage>>
    {
        private readonly IFreelancerDataFetcher _freelancerDataFetcher;
        private readonly IGigPlatformDataFetcher _gigPlatformDataFetcher;
        private readonly RebusConfiguration _rebusConfiguration;
        private readonly IDocumentStore _documentStore;
        private readonly IBus _bus;
        private readonly IMessageContext _messageContext;
        private readonly ILogger<FetchDataForPlatformConnectionHandler> _logger;

        public FetchDataForPlatformConnectionHandler(IFreelancerDataFetcher freelancerDataFetcher,
            IGigPlatformDataFetcher gigPlatformDataFetcher, IOptions<RebusConfiguration> rebusOptions,
            IDocumentStore documentStore, IBus bus, IMessageContext messageContext,
            ILogger<FetchDataForPlatformConnectionHandler> logger)
        {
            _freelancerDataFetcher = freelancerDataFetcher;
            _gigPlatformDataFetcher = gigPlatformDataFetcher;
            _rebusConfiguration = rebusOptions.Value;
            _documentStore = documentStore;
            _bus = bus;
            _messageContext = messageContext;
            _logger = logger;
        }

        public async Task Handle(FetchDataForPlatformConnectionMessage message)
        {
            using var _ = _logger.BeginNamedScopeWithMessage(nameof(DataFetchCompleteHandler),
                _messageContext.Message.GetMessageId(),
                (LoggerPropertyNames.PlatformId, message.PlatformId),
                (LoggerPropertyNames.UserId, message.UserId),
                (LoggerPropertyNames.PlatformIntegrationType, message.PlatformIntegrationType));

            _logger.LogInformation("Will start data fetching data for platform and user.");

            var cancellationToken = _messageContext.GetCancellationToken();

            using var session = _documentStore.OpenAsyncSession();
            var user = await session.LoadAsync<User>(message.UserId, cancellationToken);

            var syncLog = new DataSyncLog(user.Id, message.PlatformId);

            using var __ = _logger.BeginPropertyScope((LoggerPropertyNames.DataSyncLogId, syncLog.ExternalId));

            var platformConnection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == message.PlatformId);
            if (platformConnection == null)
            {
                await LogErrorAndForwardMessageToErrorQueue("Platform connection for user for the given platform does not exist. Will forward message to error queue.");
                return;
            }

            var connectionInfo = platformConnection.ConnectionInfo;

            
            await session.StoreAsync(syncLog);

            try
            {
                switch (message.PlatformIntegrationType)
                {
                    case PlatformIntegrationType.AirbnbIntegration:
                    case PlatformIntegrationType.UpworkIntegration:
                    case PlatformIntegrationType.Manual:
                        var logMessage1 = "Data fetch for given platform integration type not implemented. Will forward message to error queue.";
                        await LogErrorAndForwardMessageToErrorQueue(logMessage1);
                        syncLog.Steps.Add(new DataSyncStep(DataSyncStepType.PlatformDataFetch, DataSyncStepState.Failed, logMessage: logMessage1));
                        await session.SaveChangesAsync(cancellationToken);
                        return;
                    case PlatformIntegrationType.GigDataPlatformIntegration:
                        var oauthOrEmailPlatformConnectionInfo = OAuthOrEmailPlatformConnectionInfo.FromIPlatformConnectionInfo(connectionInfo);
                        oauthOrEmailPlatformConnectionInfo = await _gigPlatformDataFetcher.StartDataFetch(message.UserId,
                            message.PlatformId, oauthOrEmailPlatformConnectionInfo,
                            platformConnection, syncLog, cancellationToken);
                        connectionInfo = OAuthOrEmailPlatformConnectionInfo.FromOAuthOrEmailPlatformConnectionInfo(oauthOrEmailPlatformConnectionInfo, connectionInfo);
                        break;
                    case PlatformIntegrationType.FreelancerIntegration:
                        connectionInfo = await _freelancerDataFetcher.StartDataFetch(message.UserId, message.PlatformId,
                            (OAuthPlatformConnectionInfo) connectionInfo, platformConnection, syncLog, cancellationToken);
                        syncLog.Steps.Add(new DataSyncStep(DataSyncStepType.PlatformDataFetch, DataSyncStepState.Started));
                        break;
                    default:
                        var logMessage2 = "Unrecognized platform integration type. Will forward message to error queue.";
                        await LogErrorAndForwardMessageToErrorQueue(logMessage2);
                        syncLog.Steps.Add(new DataSyncStep(DataSyncStepType.PlatformDataFetch, DataSyncStepState.Failed, logMessage: logMessage2));
                        await session.SaveChangesAsync(cancellationToken);
                        return;
                }
            }
            catch (UnsupportedPlatformConnectionAuthenticationTypeException ex)
            {
                var logMessage = "Unsupported connection authentication type. Will forward message to error queue.";
                await LogErrorAndForwardMessageToErrorQueue(
                    "Unsupported connection authentication type. Will forward message to error queue.", ex);
                syncLog.Steps.Add(new DataSyncStep(DataSyncStepType.PlatformDataFetch, DataSyncStepState.Failed, logMessage: logMessage));
                await session.SaveChangesAsync(cancellationToken);
                return;
            }

            if (connectionInfo.IsDeleted)
            {
                //we no longer have a connection to the platform for the given user.
                _logger.LogWarning("The connection is no longer valid and will be marked as deleted.");
            }

            platformConnection.ConnectionInfo = connectionInfo;
            await session.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Data fetch successfully initialized.");
        }

        private async Task LogErrorAndForwardMessageToErrorQueue(string logMessage, Exception e = null, params object[] loggerArgs)
        {
            _logger.LogError(e, logMessage, loggerArgs);
            await _bus.Advanced.TransportMessage.Forward(_rebusConfiguration.ErrorQueueName);
        }

        public async Task Handle(IFailed<FetchDataForPlatformConnectionMessage> message)
        {
            using var _ = _logger.BeginNamedScopeWithMessage(nameof(DataFetchCompleteHandler),
                _messageContext.Message.GetMessageId(),
                (LoggerPropertyNames.PlatformId, message.Message.PlatformId),
                (LoggerPropertyNames.UserId, message.Message.UserId),
                (LoggerPropertyNames.PlatformIntegrationType, message.Message.PlatformIntegrationType));

            var topException = message.Exceptions.FirstOrDefault();
            var deferTime = TimeSpan.FromSeconds(60);

            _logger.LogError(topException, "Handling of message failed. Will defer from {deferTime} seconds and try again.", deferTime);

            await _bus.DeferLocal(deferTime, message.Message);
        }
    }
}
