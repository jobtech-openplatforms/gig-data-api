using System;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Clients;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.IoC
{
    public static class GigPlatformServiceCollectionExtension
    {
        public static IServiceCollection AddGigPlatformDataFetch(this IServiceCollection collection, IConfiguration configuration)
        {
            var gigPlatformConfig = configuration.GetSection("PlatformIntegrations").GetSection("GigPlatform");

            collection.Configure<Options>(options =>
                {
                    options.GigPlatformSecretKey = gigPlatformConfig.GetValue<string>("SecretKey");
                    options.ApiEndpointUri = gigPlatformConfig.GetValue<string>("ApiEndpointUri");
                });

            collection.AddHttpClient<GigPlatformApiClient>(client =>
            {
                client.BaseAddress = new Uri(gigPlatformConfig["ApiEndpointUri"]);
                client.DefaultRequestHeaders.Add("User-Agent", "CVData");
                client.DefaultRequestHeaders.Add("X-Api-Key", gigPlatformConfig["SecretKey"]);
            });

            collection.AddTransient<IGigPlatformDataFetcher, GigPlatformDataFetcher>();
            collection.AddTransient<IntermittentDataManager>();

            return collection;
        }
    }
}
