using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class Reputation
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; }
        [JsonProperty("last3months")]
        public ReputationDetails LastThreeMonths { get; set; }
        [JsonProperty("last12months")]
        public ReputationDetails LastTwelveMonths { get; set; }
        [JsonProperty("entire_history")]
        public ReputationDetails EntireHistory { get; set; }

    }
}