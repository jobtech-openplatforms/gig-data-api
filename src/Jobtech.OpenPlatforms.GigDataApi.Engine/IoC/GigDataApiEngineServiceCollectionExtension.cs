using System;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.IoC;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.IoC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.IoC
{
    public static class GigDataApiEngineServiceCollectionExtension
    {
        public static IServiceCollection AddGigDataApiEngine(this IServiceCollection collection)
        {
            collection.AddTransient<IPlatformManager, PlatformManager>();
            collection.AddTransient<IPlatformConnectionManager, PlatformConnectionManager>();
            collection.AddTransient<IUserManager, UserManager>();
            collection.AddTransient<IEmailValidatorManager, EmailValidatorManager>();
            collection.AddTransient<IAppManager, AppManager>();
            collection.AddTransient<IAppNotificationManager, AppNotificationManager>();
            collection.AddTransient<IPlatformDataManager, PlatformDataManager>();
            collection.AddTransient<IMailManager, MailManager>();
            collection.AddTransient<MailManager>();

            return collection;
        }

        public static IServiceCollection AddGigDataApiEngineDataFetching(this IServiceCollection collection,
            IConfiguration configuration)
        {
            collection.AddFreelancerDataFetch(configuration);
            collection.AddGigPlatformDataFetch(configuration);

            return collection;
        }

        public static IServiceCollection AddGigDataApiEngineAuthentication(this IServiceCollection collection,
            IConfiguration configuration)
        {
            var auth0Section = configuration.GetSection("Auth0");
            var managementApiAudience = auth0Section.GetValue<string>("ManagementApiAudience");
            var audience = auth0Section.GetValue<string>("CVDataAudience");
            var clientId = auth0Section.GetValue<string>("ClientId");
            var clientSecret = auth0Section.GetValue<string>("ClientSecret");
            var managementClientId = auth0Section.GetValue<string>("ManagementClientId");
            var managementClientSecret = auth0Section.GetValue<string>("ManagementClientSecret");
            var tenantDomain = auth0Section.GetValue<string>("TenantDomain");
            var mobileBankIdConnectionName = auth0Section.GetValue<string>("MobileBankIdConnectionName");
            var databaseConnectionName = auth0Section.GetValue<string>("DatabaseConnectionName");

            collection.Configure<Auth0Configuration>(options =>
            {
                options.TenantDomain = tenantDomain;
                options.ManagementApiAudience = managementApiAudience;
                options.Audience = audience;
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.ManagementClientId = managementClientId;
                options.ManagementClientSecret = managementClientSecret;
                options.MobileBankIdConnectionName = mobileBankIdConnectionName;
                options.DatabaseConnectionName = databaseConnectionName;
            });

            collection.AddHttpClient<Auth0ManagementApiHttpClient>(
                client => { client.BaseAddress = new Uri(tenantDomain); });

            var amazonSESSection = configuration.GetSection("AmazonSES");
            collection.Configure<AmazonSESConfiguration>(c => 
            {
                c.AccessKeyId = amazonSESSection.GetValue<string>("AccessKeyId");
                c.SecretKey = amazonSESSection.GetValue<string>("SecretKey");
            });

            return collection;
        }

        public static IServiceCollection AddGigDataApiEnginePlatformAuthentication(this IServiceCollection collection,
            IConfiguration configuration)
        {
            collection.AddFreelancerAuthentication(configuration);

            return collection;
        }

        public class Auth0Configuration
        {
            public string TenantDomain { get; set; }
            public string ClientSecret { get; set; }
            public string ClientId { get; set; }
            public string ManagementClientId { get; set; }
            public string ManagementClientSecret { get; set; }
            public string ManagementApiAudience { get; set; }
            public string Audience { get; set; }
            public string MobileBankIdConnectionName { get; set; }
            public string DatabaseConnectionName { get; set; }
        }
    }
}
