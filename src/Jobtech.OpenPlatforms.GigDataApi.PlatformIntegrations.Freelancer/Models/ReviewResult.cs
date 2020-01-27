using System.Collections.Generic;
using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class ReviewResult
    {
        public IEnumerable<Review> Reviews { get; set; }
        [JsonIgnore] //this is parsed separately
        public IEnumerable<ReviewResultUser> Users { get; set; }
    }

    public class ReviewResultUser
    {
        [JsonIgnore]
        public int Id { get; set; }
        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
        [JsonProperty("avatar_cdn")]
        public string AvatarCdn { get; set; }
    }
}