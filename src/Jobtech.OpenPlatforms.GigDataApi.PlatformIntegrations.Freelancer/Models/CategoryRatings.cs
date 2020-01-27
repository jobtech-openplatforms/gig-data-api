using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class CategoryRatings
    {
        public decimal Communication { get; set; }
        public decimal Expertise { get; set; }
        [JsonProperty("hire_again")]
        public decimal HireAgain { get; set; }
        public decimal Quality { get; set; }
        public decimal Professionalism { get; set; }
    }
}