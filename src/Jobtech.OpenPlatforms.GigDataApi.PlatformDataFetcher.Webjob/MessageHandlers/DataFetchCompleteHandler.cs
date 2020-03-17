using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Extensions;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Messages;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Retry.Simple;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class DataFetchCompleteHandler : IHandleMessages<DataFetchCompleteMessage>, IHandleMessages<IFailed<DataFetchCompleteMessage>>
    {
        private readonly IDocumentStore _documentStore;
        private readonly IPlatformDataManager _platformDataManager;
        private readonly IAppNotificationManager _appNotificationManager;
        private readonly IBus _bus;
        private readonly IMessageContext _messageContext;
        private readonly ILogger<DataFetchCompleteHandler> _logger;

        public DataFetchCompleteHandler(IPlatformDataManager platformDataManager,
            IAppNotificationManager appNotificationManager, IDocumentStore documentStore, IBus bus,
            IMessageContext messageContext, ILogger<DataFetchCompleteHandler> logger)
        {
            _platformDataManager = platformDataManager;
            _appNotificationManager = appNotificationManager;
            _documentStore = documentStore;
            _bus = bus;
            _messageContext = messageContext;
            _logger = logger;
        }

        public async Task Handle(DataFetchCompleteMessage message)
        {
            using var loggerScope = _logger.BeginNamedScopeWithMessage(nameof(DataFetchCompleteHandler),
                _messageContext.Message.GetMessageId(), 
                (LoggerPropertyNames.PlatformId, message.PlatformId),
                (LoggerPropertyNames.UserId, message.UserId));

            _logger.LogInformation("Fetching data from platform completed for user. Will process result and notify subscribing applications.");

            var cancellationToken = _messageContext.GetCancellationToken();

            using var session = _documentStore.OpenAsyncSession();
            var (platformDataId, platformConnection) = await HandleFetchDataResult(message.UserId, message.PlatformId,
                message.Result, session, cancellationToken);

            using var innerLoggingScope =
                _logger.BeginPropertyScope((LoggerPropertyNames.PlatformDataId, platformDataId));

            _logger.LogInformation("Result processed. Will notify {NoOfNotificationInfos} applications.",
                platformConnection.ConnectionInfo.NotificationInfos.Count);

            await session.SaveChangesAsync(cancellationToken);

            await _appNotificationManager.NotifyPlatformConnectionDataUpdate(message.UserId,
                platformConnection.ConnectionInfo.NotificationInfos.Select(ni => ni.AppId).ToList(),
                platformConnection.PlatformId, platformConnection.ExternalPlatformId, platformConnection.PlatformName, platformDataId, session, cancellationToken);

            _logger.LogInformation("Applications have been notified.");
        }

        private async Task<(string PlatformDataId, PlatformConnection platformConnection)> HandleFetchDataResult(string userId, string platformId, 
            PlatformDataFetchResult result, IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var user = await session.LoadAsync<User>(userId, cancellationToken);

            var platformData = await _platformDataManager.AddPlatformData(userId, platformId, result.NumberOfGigs,
                result.PeriodStart,
                result.PeriodEnd, result.Ratings, result.AverageRating, result.Reviews, result.Achievements,
                result.RawData, session, cancellationToken);

            var platformConnection = user.PlatformConnections.Single(pc => pc.PlatformId == platformId);
            platformConnection.MarkAsDataFetchSuccessful();

            return (platformData.Id, platformConnection);
        }

        public async Task Handle(IFailed<DataFetchCompleteMessage> message)
        {
            using var loggerScope = _logger.BeginNamedScopeWithMessage(nameof(DataFetchCompleteHandler),
                _messageContext.Message.GetMessageId(), 
                (LoggerPropertyNames.PlatformId, message.Message.PlatformId),
                (LoggerPropertyNames.UserId, message.Message.UserId));

            var topException = message.Exceptions.FirstOrDefault();
            var deferTime = TimeSpan.FromSeconds(60);

            _logger.LogError(topException, "Handling of message failed. Will defer from {deferTime} seconds and try again.", deferTime);

            await _bus.DeferLocal(deferTime, message.Message);
        }
    }
}
