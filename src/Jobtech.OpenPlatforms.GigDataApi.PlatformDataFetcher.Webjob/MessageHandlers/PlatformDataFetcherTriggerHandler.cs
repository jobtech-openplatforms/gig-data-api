using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Extensions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Indexes;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Messages;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class PlatformDataFetcherTriggerHandler: IHandleMessages<PlatformDataFetcherTriggerMessage>
    {
        private readonly IPlatformManager _platformManager;
        private readonly IDocumentStore _documentStore;
        private readonly IBus _bus;
        private readonly IMessageContext _messageContext;
        private readonly ILogger<PlatformDataFetcherTriggerHandler> _logger;

        public PlatformDataFetcherTriggerHandler(IPlatformManager platformManager, IDocumentStore documentStore,
            IBus bus, IMessageContext messageContext, ILogger<PlatformDataFetcherTriggerHandler> logger)
        {
            _platformManager = platformManager;
            _documentStore = documentStore;
            _bus = bus;
            _messageContext = messageContext;
            _logger = logger;
        }

        public async Task Handle(PlatformDataFetcherTriggerMessage message)
        {
            using var loggerScope = _logger.BeginNamedScopeWithMessage(nameof(DataFetchCompleteHandler),
                _messageContext.Message.GetMessageId());

            _logger.LogInformation("Will check if any platform connection is up for data fetching.");

            var cancellationToken = _messageContext.GetCancellationToken();

            using var session = _documentStore.OpenAsyncSession();
            var platformConnectionsToFetchDataForPerUser =
                await GetPlatformConnectionsReadyForDataFetch(session, cancellationToken);
            var platforms = await _platformManager.GetPlatforms(
                platformConnectionsToFetchDataForPerUser.SelectMany(kvp => kvp.Value).Select(pc => pc.PlatformId).Distinct()
                    .ToList(), session);

            _logger.LogInformation(
                "Found {NoOfUsers} users that have at least one platform connection to trigger data fetch for.", platformConnectionsToFetchDataForPerUser.Count);

            foreach (var kvp in platformConnectionsToFetchDataForPerUser)
            {
                var userId = kvp.Key;
                using var innerLoggingScope1 = _logger.BeginPropertyScope((LoggerPropertyNames.UserId, userId));

                _logger.LogInformation("Will trigger data fetches for {NoOfPlatformConnections} distinct platforms for user.", kvp.Value.Count());

                foreach (var platformConnection in kvp.Value)
                {
                    using var innerLoggingScope2 = _logger.BeginPropertyScope(
                        (LoggerPropertyNames.PlatformId, platformConnection.PlatformId),
                        (LoggerPropertyNames.PlatformName, platformConnection.PlatformName));

                    _logger.LogInformation(
                        "Will trigger data fetch for platform. LastSuccessfulDataFetch: {LastSuccessfulDataFetch}", platformConnection.LastSuccessfulDataFetch);

                    platformConnection.MarkAsDataFetchStarted();
                    var platform = platforms[platformConnection.PlatformId];
                    var fetchDataMessage = new FetchDataForPlatformConnectionMessage(userId,
                        platformConnection.PlatformId, platform.IntegrationType);
                    await _bus.SendLocal(fetchDataMessage);
                }
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        private static async Task<IList<KeyValuePair<string, IEnumerable<PlatformConnection>>>>
            GetPlatformConnectionsReadyForDataFetch(IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;

            var smallestExistingDataPullInterval = await session
                .Query<Users_ByPlatformConnectionPossiblyRipeForDataFetch.Result,
                    Users_ByPlatformConnectionPossiblyRipeForDataFetch>()
                .OrderBy(u => u.MinimumDataPullIntervalInSeconds).Take(1)
                .Select(u => u.MinimumDataPullIntervalInSeconds).FirstOrDefaultAsync(cancellationToken);

            if (!smallestExistingDataPullInterval.HasValue)
            {
                return new List<KeyValuePair<string, IEnumerable<PlatformConnection>>>();
            }

            var cutoff = now - TimeSpan.FromSeconds(smallestExistingDataPullInterval.Value);

            var userIdsThatPossibleHavePlatformConnectionsRipeForDataFetch = await session
                .Query<Users_ByPlatformConnectionPossiblyRipeForDataFetch.Result,
                    Users_ByPlatformConnectionPossiblyRipeForDataFetch>()
                .Where(u => u.EarliestPlatformConnectionDataFetchCompletion == DateTimeOffset.MinValue ||
                            u.EarliestPlatformConnectionDataFetchCompletion.Value < cutoff)
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);

            var usersThatPossibleHavePlatformConnectionsRipeForDataFetch =
                await session.LoadAsync<User>(userIdsThatPossibleHavePlatformConnectionsRipeForDataFetch,
                    cancellationToken);

            var userIdsToPlatformConnectionsThatShouldBeUpdated =
                usersThatPossibleHavePlatformConnectionsRipeForDataFetch.Values
                    .Where(u => u.PlatformConnections.Any(pc => IsPlatformConnectionRipeForUpdate(pc, now)))
                    .Select(u => new KeyValuePair<string, IEnumerable<PlatformConnection>>(u.Id,
                        u.PlatformConnections.Where(pc => IsPlatformConnectionRipeForUpdate(pc, now)).ToList()))
                    .ToList();

            return userIdsToPlatformConnectionsThatShouldBeUpdated;
        }

        private static bool IsPlatformConnectionRipeForUpdate(PlatformConnection pc, DateTimeOffset now)
        {
            return !pc.ConnectionInfo.IsDeleted && 
                   pc.DataPullIntervalInSeconds.HasValue &&
                   (!pc.LastDataFetchAttemptStart.HasValue ||
                   (pc.LastDataFetchAttemptCompleted.HasValue &&                            //we did complete the last update attempt and it was more then data pull interval ago 
                    now.Subtract(pc.LastDataFetchAttemptCompleted.Value).TotalSeconds >=
                    pc.DataPullIntervalInSeconds.Value) ||
                   (!pc.LastDataFetchAttemptCompleted.HasValue &&                           //we did not complete the last update attempt and the attempt was started more then data pull interval ago
                    now.Subtract(pc.LastDataFetchAttemptStart.Value).TotalSeconds >= 
                    pc.DataPullIntervalInSeconds.Value)); 
        }
    }
}
