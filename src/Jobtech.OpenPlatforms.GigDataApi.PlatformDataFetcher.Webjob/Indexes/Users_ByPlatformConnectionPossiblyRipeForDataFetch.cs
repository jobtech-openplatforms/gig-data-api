using System;
using System.Linq;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Raven.Client.Documents.Indexes;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Indexes
{
    public class Users_ByPlatformConnectionPossiblyRipeForDataFetch: AbstractIndexCreationTask<User, Users_ByPlatformConnectionPossiblyRipeForDataFetch.Result>
    {
        public class Result
        {
            public string UserId { get; set; }
            public int? MinimumDataPullIntervalInSeconds { get; set; }
            public DateTimeOffset? EarliestPlatformConnectionDataFetchCompletion { get; set; }
        }

        public Users_ByPlatformConnectionPossiblyRipeForDataFetch()
        {
            Map = users => from user in users
                from pc in user.PlatformConnections
                select new
                {
                    UserId = user.Id,
                    EarliestPlatformConnectionDataFetchCompletion = pc.LastDataFetchAttemptCompleted,
                    MinimumDataPullIntervalInSeconds = pc.DataPullIntervalInSeconds
                };

            Reduce = results => from result in results
                group result by result.UserId
                into g
                select new
                {
                    UserId = g.Key,
                    EarliestPlatformConnectionDataFetchCompletion =
                        g.Min(x => x.EarliestPlatformConnectionDataFetchCompletion) ?? DateTimeOffset.MinValue,
                    MinimumDataPullIntervalInSeconds = g.Min(x => x.MinimumDataPullIntervalInSeconds) ?? -1
                };
        }
    }
}
