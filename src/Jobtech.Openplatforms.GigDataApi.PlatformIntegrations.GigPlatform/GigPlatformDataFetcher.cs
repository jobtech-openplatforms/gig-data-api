using System;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Extensions;
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
        Task CompleteDataFetching(string userId, string platformId, PlatformDataFetchResult dataFetchResult,
            CancellationToken cancellationToken = default);
    }

    public class GigPlatformDataFetcher : DataFetcherBase<OAuthOrEmailPlatformConnectionInfo>, IGigPlatformDataFetcher
    {
        private readonly IntermittentDataManager _intermittentDataManager;
        private readonly ILogger<GigPlatformDataFetcher> _logger;
        private readonly GigPlatformApiClient _apiClient;

        public GigPlatformDataFetcher(IntermittentDataManager intermittentDataManager, GigPlatformApiClient apiClient,
            IBus bus, ILogger<GigPlatformDataFetcher> logger) : base(bus)
        {
            _intermittentDataManager = intermittentDataManager;
            _apiClient = apiClient;
            _logger = logger;
        }

        public new async Task<OAuthOrEmailPlatformConnectionInfo> StartDataFetch(string userId, string platformId,
            OAuthOrEmailPlatformConnectionInfo connectionInfo, PlatformConnection platformConnection,
            CancellationToken cancellationToken = default)
        {
            using var loggerScope = _logger.BeginPropertyScope((LoggerPropertyNames.UserId, userId),
                (LoggerPropertyNames.PlatformId, platformId), (LoggerPropertyNames.PlatformName, platformConnection.PlatformName));

            _logger.LogInformation("Will start data fetch from a Gig platform integrated platform for user.");

            if (connectionInfo.IsOAuthAuthentication)
            {
                _logger.LogError("Oauth connection not yet supported in Gig Platform API. Will throw.");
                throw new UnsupportedPlatformConnectionAuthenticationTypeException("Oauth connection not yet supported in Gig Platform API");
            }

            var result = await _apiClient.RequestLatest(platformConnection.ExternalPlatformId, connectionInfo.Email, cancellationToken);

            _logger.LogInformation(
                $"Successfully requested data fetch against Platform API for platform.");

            if (!result.Success)
            {
                //we interpret non success result as a situation where the user has removed consent for us to read the information.
                _logger.LogInformation("User has does not consent that we fetch information. Will signal that the connection should be removed.");
                await CompleteDataFetchWithConnectionRemoved(userId, platformId, cancellationToken);
                return new OAuthOrEmailPlatformConnectionInfo(connectionInfo.Email) {IsDeleted = true};
            }

            using var innerLoggerScope =
                _logger.BeginPropertyScope((LoggerPropertyNames.GigPlatformApiRequestId, result.RequestId));

            await _intermittentDataManager.RegisterRequestData(result.RequestId, userId, platformId);

            _logger.LogInformation("Data fetch successfully started.");

            return connectionInfo;
        }

        public async Task CompleteDataFetching(string userId, string platformId,
            PlatformDataFetchResult dataFetchResult, CancellationToken cancellationToken = default)
        {
            await CompleteDataFetch(userId, platformId, dataFetchResult, cancellationToken);
        }
    }
}
