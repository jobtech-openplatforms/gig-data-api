using System;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Clients;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.IoC
{
    public static class FreelancerServiceCollectionExtensions
    {
        public static IServiceCollection AddFreelancerDataFetch(this IServiceCollection collection,
            IConfiguration configuration)
        {
            var freelancerConfig = configuration.GetSection("PlatformIntegrations").GetSection("Freelancer");

            collection.Configure<Options>(options =>
            {
                options.AppId = freelancerConfig.GetValue<string>("AppId");
                options.ClientSecret = freelancerConfig.GetValue<string>("ClientSecret");
                options.ApiEndpointUri = freelancerConfig.GetValue<string>("ApiEndpointUri");
            });

            collection.AddHttpClient<FreelancerApiClient>(client =>
            {
                client.BaseAddress = new Uri(freelancerConfig.GetValue<string>("ApiEndpointUri"));
                client.DefaultRequestHeaders.Add("User-Agent", "CVData");
            });

            collection.AddTransient<IFreelancerDataFetcher, FreelancerDataFetcher>();

            return collection;
        }

        public static IServiceCollection AddFreelancerAuthentication(this IServiceCollection collection,
            IConfiguration configuration)
        {
            var freelancerConfig = configuration.GetSection("PlatformIntegrations").GetSection("Freelancer");

            collection.Configure<Options>(options =>
            {
                options.AppId = freelancerConfig.GetValue<string>("AppId");
                options.ClientSecret = freelancerConfig.GetValue<string>("ClientSecret");
                options.AuthEndpointUri = freelancerConfig.GetValue<string>("AuthEndpointUri");
                options.AuthRedirectUri = freelancerConfig.GetValue<string>("AuthRedirectUri");
            });

            collection.AddHttpClient<FreelancerOAuthClient>(
                client =>
                {
                    client.BaseAddress = new Uri(freelancerConfig.GetValue<string>("AuthEndpointUri"));
                    client.DefaultRequestHeaders.Add("User-Agent", "CVData");
                });

            collection.AddTransient<IFreelancerAuthenticator, FreelancerAuthenticator>();

            return collection;
        }
    }
}
