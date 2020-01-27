using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class Badge
    {
        public int Id { get; set; }
        public string Name { get; set; }
        [JsonProperty("icon_url")]
        public string IconUrl { get; set; }
        public string Description { get; set; }
        [JsonProperty("time_awarded")]
        public long TimeAwarded { get; set; }
    }
}