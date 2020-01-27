using System.Collections.Generic;
using Newtonsoft.Json;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models
{
    public class UserInfoResult
    {
        public int Id { get; set; }
        [JsonProperty("registration_date")]
        public int RegistrationDate { get; set; }
        public Reputation Reputation { get; set; }
        public IEnumerable<Job> Jobs { get; set; }
        public IEnumerable<Qualification> Qualifications { get; set; }
        public IEnumerable<Badge> Badges { get; set; }
    }
}