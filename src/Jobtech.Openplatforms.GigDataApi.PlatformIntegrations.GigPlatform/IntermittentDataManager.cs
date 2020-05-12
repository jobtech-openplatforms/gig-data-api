using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform
{
    public class IntermittentDataManager
    {
        private readonly IDistributedCache _cache;

        public IntermittentDataManager(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task RegisterRequestData(string requestId, string userId, string platformId, string syncLogId)
        {
            var dataObj = new RequestData {UserId = userId, PlatformId = platformId, SyncLogId = syncLogId};
            await _cache.SetStringAsync(requestId, JsonConvert.SerializeObject(dataObj));
        }

        public async Task<(string UserId, string PlatformId, string syncLogId)> GetRequestData(string requestId)
        {
            var requestDataStr = await _cache.GetStringAsync(requestId);
            if (requestDataStr == null)
            {
                throw new CacheDataNotFoundException($"Key: {requestId}");
            }
            var requestData = JsonConvert.DeserializeObject<RequestData>(requestDataStr);
            return (requestData.UserId, requestData.PlatformId, requestData.SyncLogId);
        }
    }

    class RequestData
    {
        public string UserId { get; set; }
        public string PlatformId { get; set; }
        public string SyncLogId { get; set; }
    }
}
