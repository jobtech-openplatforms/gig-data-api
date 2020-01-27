using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class ReviewContext
    {
        [JsonProperty("review_type")]
        public string ReviewType { get; set; }
        [JsonProperty("context_id")]
        public int ContextId { get; set; }
        [JsonProperty("context_name")]
        public string ContextName { get; set; }
    }
}