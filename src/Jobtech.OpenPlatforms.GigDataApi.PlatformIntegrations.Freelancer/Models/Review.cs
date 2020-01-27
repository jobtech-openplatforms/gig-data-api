using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class Review
    {
        [JsonProperty("to_user_id")]
        public int ToUserId { get; set; }
        [JsonProperty("from_user_id")]
        public int FromUserId { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        [JsonProperty("time_submitted")]
        public int TimeSubmitted { get; set; }
        public decimal Rating { get; set; }
        public string Description { get; set; }
        public decimal? PaidAmount { get; set; }
        [JsonProperty("review_context")]
        public ReviewContext ReviewContext { get; set; }
    }
}