using System;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Jobtech.OpenPlatforms.GigDataApi.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var webHost = CreateWebHostBuilder(args).ConfigureLogging(configure =>
            {
                //configure.AddConsole();
            }).Build();

            //init database if needed
            using (var scope = webHost.Services.CreateScope())
            {
                var hostingEnvironment = scope.ServiceProvider.GetRequiredService<IHostingEnvironment>();
                if (hostingEnvironment.IsDevelopment())
                {
                    var dataStore = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
                    var platformManager = scope.ServiceProvider.GetRequiredService<IPlatformManager>();
                    var appManager = scope.ServiceProvider.GetRequiredService<IAppManager>();
                    var optionsMonitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<Options>>();
                    var testDataSetup = new BaseTestDataSetup(platformManager, appManager, optionsMonitor, dataStore);
                    await testDataSetup.SetupDataAsNeeded();
                }
            }

            await webHost.RunAsync();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }

    public class BaseTestDataSetup
    {
        private readonly IPlatformManager _platformManager;
        private readonly IAppManager _appManager;
        private readonly IDocumentStore _dataStore;
        private readonly Options _options;
        
        public BaseTestDataSetup(IPlatformManager platformManager, IAppManager appManager, IOptionsMonitor<Options> options, IDocumentStore dataStore)
        {
            _platformManager = platformManager;
            _appManager = appManager;
            _options = options.CurrentValue;
            _dataStore = dataStore;
        }

        public async Task SetupDataAsNeeded()
        {
            using (var session = _dataStore.OpenAsyncSession())
            {
                var existingPlatforms = await session.Query<Core.Entities.Platform>().ToListAsync();
                if (!existingPlatforms.Any())
                {
                    await CreateDefaultPlatforms(session);
                }

                var existingApps = await session.Query<App>().ToListAsync();
                if (!existingApps.Any())
                {
                    await CreateDefaultApps(session);
                }

                await session.SaveChangesAsync();
            }
        }

        private async Task CreateDefaultPlatforms(IAsyncDocumentSession session)
        {
            await _platformManager.CreatePlatform("Gigstr", PlatformAuthenticationMechanism.Email, PlatformIntegrationType.GigDataPlatformIntegration, 
                new RatingInfo(1,5,3.5m), null, Guid.NewGuid(), null, null, session);
            await _platformManager.CreatePlatform("Freelancer", PlatformAuthenticationMechanism.Oauth2,
                PlatformIntegrationType.FreelancerIntegration, new RatingInfo(1, 5, 4m), 3600,
                _options.PlatformExternalIds["Freelancer"], null, null, session);
        }

        private async Task CreateDefaultApps(IAsyncDocumentSession session)
        {
            await _appManager.CreateApp("MyGigData", "g7pB2vgb5l8BQdYN56J3HP3VGYh9Bv3P", "VFG6CJE8JE7M4PGCLXR7",
                "https://us-central1-my-digital-backpack.cloudfunctions.net/updatedata",
                "https://us-central1-my-digital-backpack.cloudfunctions.net/updateemailverification",
                session);
        }
    }
}
