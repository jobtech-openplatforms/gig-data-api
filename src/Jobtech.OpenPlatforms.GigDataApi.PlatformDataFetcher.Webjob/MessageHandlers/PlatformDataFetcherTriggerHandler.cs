using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Rebus.Handlers;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class PlatformDataFetcherTriggerHandler: IHandleMessages<PlatformDataFetcherTriggerMessage>
    {
        private readonly IPlatformManager _platformManager;
        private readonly IDocumentStore _documentStore;
        private readonly IBus _bus;
        private readonly ILogger<PlatformDataFetcherTriggerHandler> _logger;

        public PlatformDataFetcherTriggerHandler(IPlatformManager platformManager, IDocumentStore documentStore,
            IBus bus, ILogger<PlatformDataFetcherTriggerHandler> logger)
        {
            _platformManager = platformManager;
            _documentStore = documentStore;
            _bus = bus;
            _logger = logger;
        }

        public async Task Handle(PlatformDataFetcherTriggerMessage message)
        {
            _logger.LogInformation("Timer triggered Data Fetch Scheduler triggered");

            using (var session = _documentStore.OpenAsyncSession())
            {
                var platformConnectionsToFetchDataFor = await GetPlatformConnectionsReadyForDataFetch(session);
                var platforms = await _platformManager.GetPlatforms(
                    platformConnectionsToFetchDataFor.SelectMany(kvp => kvp.Value).Select(pc => pc.PlatformId).Distinct()
                        .ToList(), session);

                _logger.LogInformation($"Found {platformConnectionsToFetchDataFor.Count} platform connection to trigger data fetch for.");

                foreach (var kvp in platformConnectionsToFetchDataFor)
                {
                    var userId = kvp.Key;

                    foreach (var platformConnection in kvp.Value)
                    {
                        _logger.LogInformation(
                            $"Will trigger data fetch for platform connection. User: {userId}, PlatformId: {platformConnection.PlatformId}, LastSuccessfulDataFetch: {platformConnection.LastSuccessfulDataFetch}");

                        platformConnection.MarkAsDataFetchStarted();
                        var platform = platforms[platformConnection.PlatformId];
                        var fetchDataMessage = new FetchDataForPlatformConnectionMessage(userId,
                            platformConnection.PlatformId, platform.IntegrationType, platformConnection.ConnectionInfo);
                        await _bus.SendLocal(fetchDataMessage);
                    }
                }

                await session.SaveChangesAsync();
            }
        }

        private async Task<IList<KeyValuePair<string, IEnumerable<PlatformConnection>>>> GetPlatformConnectionsReadyForDataFetch(IAsyncDocumentSession session)
        {
            var now = DateTimeOffset.UtcNow;

            var smallestExistingDataPullInterval = await session
                .Query<Users_ByPlatformConnectionPossiblyRipeForDataFetch.Result,
                    Users_ByPlatformConnectionPossiblyRipeForDataFetch>()
                .OrderBy(u => u.MinimumDataPullIntervalInSeconds).Take(1)
                .Select(u => u.MinimumDataPullIntervalInSeconds).FirstOrDefaultAsync();

            if (!smallestExistingDataPullInterval.HasValue)
            {
                return new List<KeyValuePair<string, IEnumerable<PlatformConnection>>>();
            }

            var cutoff = now - TimeSpan.FromSeconds(smallestExistingDataPullInterval.Value);

            var userIdsThatPossibleHavePlatformConnectionsRipeForDataFetch = await session.Query<Users_ByPlatformConnectionPossiblyRipeForDataFetch.Result, Users_ByPlatformConnectionPossiblyRipeForDataFetch>()
                .Where(u => u.EarliestPlatformConnectionDataFetchCompletion == DateTimeOffset.MinValue || u.EarliestPlatformConnectionDataFetchCompletion.Value < cutoff)
                .Select(u => u.UserId)
                .ToListAsync();

            var usersThatPossibleHavePlatformConnectionsRipeForDataFetch =
                await session.LoadAsync<User>(userIdsThatPossibleHavePlatformConnectionsRipeForDataFetch);

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
            return pc.DataPullIntervalInSeconds.HasValue &&
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
