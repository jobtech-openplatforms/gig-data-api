using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Clients
{
    public class GigPlatformApiClient
    {
        private readonly ILogger<GigPlatformApiClient> _logger;

        public GigPlatformApiClient(HttpClient client, ILogger<GigPlatformApiClient> logger)
        {
            Client = client;
            _logger = logger;
        }

        public HttpClient Client { get; }

        public async Task<RequestLatestResult> RequestLatest(Guid externalPlatformId, string userToken, CancellationToken cancellationToken = default)
        {
            var payload = new {UserName = userToken, PlatformId = externalPlatformId};
            var jsonPayload = JsonConvert.SerializeObject(payload);

            HttpResponseMessage httpResult;

            try
            {
                httpResult = await Client.PostAsync($"platform/latest", new StringContent(jsonPayload, Encoding.UTF8, "application/json"), cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error communicating with gig platform service. Will throw.");
                throw new ExternalServiceErrorException("Error communicating with gig platform service.", e);
            }

            var strResult = await httpResult.Content.ReadAsStringAsync();
            if (!httpResult.IsSuccessStatusCode)
            {
                _logger.LogError("Got non success status code {HttpStatusCode}. Will throw.", httpResult.StatusCode);
                throw new ExternalServiceErrorException($"Got non success status code ({httpResult.StatusCode})");
            }

            var result = JsonConvert.DeserializeObject<RequestLatestResult>(strResult);

            return result;
        }
    }
}
