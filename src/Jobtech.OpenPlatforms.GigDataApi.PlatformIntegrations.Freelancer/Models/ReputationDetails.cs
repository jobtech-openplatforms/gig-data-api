using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class ReputationDetails
    {
        [JsonProperty("completion_rate")]
        public decimal CompletionRate { get; set; }
        public int All { get; set; }
        [JsonProperty("incomplete_reviews")]
        public int IncompleteReviews { get; set; }
        public int Complete { get; set; }
        [JsonProperty("on_time")]
        public decimal OnTime { get; set; }
        [JsonProperty("on_budget")]
        public decimal OnBudget { get; set; }
        public decimal Positive { get; set; }
        public decimal Overall { get; set; }
        public int Reviews { get; set; }
        public int Incomplete { get; set; }
        [JsonProperty("category_ratings")]
        public CategoryRatings CategoryRatings { get; set; }
    }
}