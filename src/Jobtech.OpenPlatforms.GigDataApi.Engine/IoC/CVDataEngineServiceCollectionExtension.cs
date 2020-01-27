using System;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.IoC;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.IoC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.IoC
{
    public static class CVDataEngineServiceCollectionExtension
    {
        public static IServiceCollection AddCVDataEngine(this IServiceCollection collection,
            IConfiguration configuration)
        {
            collection.AddTransient<IPlatformManager, PlatformManager>();
            collection.AddTransient<IPlatformConnectionManager, PlatformConnectionManager>();
            collection.AddTransient<IUserManager, UserManager>();
            collection.AddTransient<IEmailValidatorManager, EmailValidatorManager>();
            collection.AddTransient<IAppManager, AppManager>();
            collection.AddTransient<IAppNotificationManager, AppNotificationManager>();
            collection.AddTransient<IPlatformDataManager, PlatformDataManager>();

            return collection;
        }

        public static IServiceCollection AddCVDataEngineDataFetching(this IServiceCollection collection,
            IConfiguration configuration)
        {
            collection.AddFreelancerDataFetch(configuration);
            collection.AddGigPlatformDataFetch(configuration);

            return collection;
        }

        public static IServiceCollection AddCVDataEngineAuthentication(this IServiceCollection collection,
            IConfiguration configuration)
        {
            var auth0Section = configuration.GetSection("Auth0");
            var managementApiAudience = auth0Section.GetValue<string>("ManagementApiAudience");
            var cvDataAudience = auth0Section.GetValue<string>("CVDataAudience");
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
                options.CVDataAudience = cvDataAudience;
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.ManagementClientId = managementClientId;
                options.ManagementClientSecret = managementClientSecret;
                options.MobileBankIdConnectionName = mobileBankIdConnectionName;
                options.DatabaseConnectionName = databaseConnectionName;
            });

            collection.AddHttpClient<Auth0ManagementApiHttpClient>(
                client => { client.BaseAddress = new Uri(tenantDomain); });

            var approveApiSection = configuration.GetSection("ApproveApi");
            var approveApiApiKey = approveApiSection.GetValue<string>("ApiKey");
            var approveApiApiUrl = approveApiSection.GetValue<string>("ApiUrl");
            var approveApiConfirmEmailCallbackUrl = approveApiSection.GetValue<string>("ConfirmEmailCallbackUrl");
            var approveApiRejectEmailCallbackUrl = approveApiSection.GetValue<string>("RejectEmailCallbackUrl");
            collection.Configure<ApproveApiConfiguration>(options =>
            {
                options.ApiKey = approveApiApiKey;
                options.ApiUrl = approveApiApiUrl;
                options.ConfirmEmailCallbackUri = approveApiConfirmEmailCallbackUrl;
                options.RejectEmailCallbackUri = approveApiRejectEmailCallbackUrl;
            });

            collection.AddHttpClient<ApproveApiHttpClient>(client => { client.BaseAddress = new Uri(approveApiApiUrl); });

            return collection;
        }

        public static IServiceCollection AddCVDataEnginePlatformAuthentication(this IServiceCollection collection,
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
            public string CVDataAudience { get; set; }
            public string MobileBankIdConnectionName { get; set; }
            public string DatabaseConnectionName { get; set; }
        }

        public class ApproveApiConfiguration
        {
            public string ApiUrl { get; set; }
            public string ApiKey { get; set; }
            public string ConfirmEmailCallbackUri { get; set; }
            public string RejectEmailCallbackUri { get; set; }
        }
    }
}
