using System;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Clients;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform
{
    public interface IGigPlatformDataFetcher : IDataFetcher<OAuthOrEmailPlatformConnectionInfo>
    {
        Task CompleteDataFetching(string userId, string platformId, PlatformDataFetchResult dataFetchResult);
    }

    public class GigPlatformDataFetcher : DataFetcherBase<OAuthOrEmailPlatformConnectionInfo>, IGigPlatformDataFetcher
    {
        private readonly IntermittentDataManager _intermittentDataManager;
        private readonly ILogger<GigPlatformDataFetcher> _logger;
        private readonly GigPlatformApiClient _apiClient;

        public GigPlatformDataFetcher(IntermittentDataManager intermittentDataManager, GigPlatformApiClient apiClient, IBus bus, ILogger<GigPlatformDataFetcher> logger) : base(bus)
        {
            _intermittentDataManager = intermittentDataManager;
            _apiClient = apiClient;
            _logger = logger;
        }

        public new async Task<OAuthOrEmailPlatformConnectionInfo> StartDataFetch(string userId, string platformId,
            OAuthOrEmailPlatformConnectionInfo connectionInfo, PlatformConnection platformConnection)
        {
            _logger.LogTrace($"Will start data fetch from Freelancer for user with id {userId}");

            if (connectionInfo.IsOAuthAuthentication)
            {
                throw new ArgumentException("Oauth connection not yet supported in Gig Platform API",
                    nameof(connectionInfo));
            }

            var result = await _apiClient.RequestLatest(platformConnection.ExternalPlatformId, connectionInfo.Email);

            _logger.LogInformation(
                $"Did request data fetch against Platform API for platform with name {platformConnection.PlatformName} and external id {platformConnection.ExternalPlatformId}. Got back the following request id: {result.RequestId}");

            if (!result.Success) //we interpret non success result as a situation where the user has removed consent for us to read the information.
            {
                _logger.LogInformation("User has does not consent that we fetch information. Will remove connection.");
                await CompleteDataFetchWithConnectionRemoved(userId, platformId);
                return new OAuthOrEmailPlatformConnectionInfo(connectionInfo.Email) {IsDeleted = true};
            }

            await _intermittentDataManager.RegisterRequestData(result.RequestId, userId, platformId);

            return connectionInfo;
        }

        public async Task CompleteDataFetching(string userId, string platformId, PlatformDataFetchResult dataFetchResult)
        {
            await CompleteDataFetch(userId, platformId, dataFetchResult);
        }
    }
}
