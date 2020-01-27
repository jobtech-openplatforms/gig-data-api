using System.Net.Http;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Clients
{
    public class FreelancerOAuthClient
    {
        public FreelancerOAuthClient(HttpClient client)
        {
            Client = client;
        }

        public HttpClient Client { get; }
    }
}