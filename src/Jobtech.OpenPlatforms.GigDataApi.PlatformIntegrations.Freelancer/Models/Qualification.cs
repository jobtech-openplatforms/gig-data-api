using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class Qualification
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string Type { get; set; }
        [JsonProperty("icon_url")]
        public string IconUrl { get; set; }
        public string Description { get; set; }
        [JsonProperty("icon_name")]
        public string IconName { get; set; }
        [JsonProperty("score_percentage")]
        public decimal ScorePercentage { get; set; }
        [JsonProperty("user_percentile")]
        public decimal UserPercentile { get; set; }

    }
}
