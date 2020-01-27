using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class ApiResult<TResult>
    {
        public string Status { get; set; }
        public TResult Result { get; set; }
        [JsonProperty("request_id")]
        public string RequestId { get; set; }
    }
}