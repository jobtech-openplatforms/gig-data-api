using System;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Extensions;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Clients;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Exceptions;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform
{
    public interface IGigPlatformDataFetcher : IDataFetcher<OAuthOrEmailPlatformConnectionInfo>
    {
        Task CompleteDataFetching(string userId, string platformId, PlatformDataFetchResult dataFetchResult,
            CancellationToken cancellationToken = default);

        Task CompleteDataFetchingWithConnectionRemoved(string userId, string platformId,
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
            using var _ = _logger.BeginPropertyScope((LoggerPropertyNames.UserId, userId),
                (LoggerPropertyNames.PlatformId, platformId), (LoggerPropertyNames.PlatformName, platformConnection.PlatformName));

            _logger.LogInformation("Will start data fetch from a Gig platform integrated platform for user.");

            if (connectionInfo.IsOAuthAuthentication)
            {
                _logger.LogError("Oauth connection not yet supported in Gig Platform API. Will throw.");
                throw new UnsupportedPlatformConnectionAuthenticationTypeException("Oauth connection not yet supported in Gig Platform API");
            }

            try
            {
                var result = await _apiClient.RequestLatest(platformConnection.ExternalPlatformId, connectionInfo.Email, cancellationToken);
                using var __ = _logger.BeginPropertyScope((LoggerPropertyNames.GigPlatformApiRequestId, result.RequestId));
                await _intermittentDataManager.RegisterRequestData(result.RequestId, userId, platformId);
            } 
            catch (ExternalServiceErrorException ex)
            {
                //Error that can occur here is that we get either a 500, which means there is something wrong with platform api server. Or we get a 404, which means that the
                //platform we asked the api to fetch data from does not exist on the api side of things. Both these cases should result in a retry. So we throw here.
                _logger.LogError(ex, "Got error when regestering data fetch. Will throw.");
                throw new GigDataPlatformApiInitiateDataFetchException();
            }


            _logger.LogInformation("Data fetch successfully started.");
            return connectionInfo;
        }

        public async Task CompleteDataFetching(string userId, string platformId,
            PlatformDataFetchResult dataFetchResult, CancellationToken cancellationToken = default)
        {
            await CompleteDataFetch(userId, platformId, dataFetchResult, cancellationToken);
        }

        public async Task CompleteDataFetchingWithConnectionRemoved(string userId, string platformId, CancellationToken cancellationToken = default)
        {
            await CompleteDataFetchWithConnectionRemoved(userId, platformId, cancellationToken);
        }
    }
}
