using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Models;
using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Clients
{
    public class GigPlatformApiClient
    {
        public GigPlatformApiClient(HttpClient client)
        {
            Client = client;
        }

        public HttpClient Client { get; }

        public async Task<RequestLatestResult> RequestLatest(Guid externalPlatformId, string userToken)
        {
            var payload = new {UserName = userToken, PlatformId = externalPlatformId};
            var jsonPayload = JsonConvert.SerializeObject(payload);

            var httpResult = await Client.PostAsync($"platform/latest", new StringContent(jsonPayload, Encoding.UTF8, "application/json") );
            var strResult = await httpResult.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<RequestLatestResult>(strResult);

            return result;
        }
    }
}
