using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Common.RavenDB;
using Jobtech.OpenPlatforms.GigDataApi.Engine.IoC;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Indexes;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Raven.Client.Documents;
using Rebus.Config;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using Rebus.ServiceProvider;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new HostBuilder();

            builder.ConfigureHostConfiguration(configHost =>
            {
                configHost.SetBasePath(Directory.GetCurrentDirectory());
                configHost.AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string>(HostDefaults.EnvironmentKey,
                            System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")),
                    })
                    .AddEnvironmentVariables();
            }).ConfigureAppConfiguration((hostContext, configApp) =>
            {
                configApp.SetBasePath(Directory.GetCurrentDirectory());
                configApp.AddJsonFile("appsettings.json", false, true);
                configApp.AddJsonFile(
                    $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                    optional: true);
                configApp.AddJsonFile("secrets/appsettings.secrets.json", optional: true);

                configApp.AddEnvironmentVariables();
            }).ConfigureWebJobs(configWebjob =>
            {
                configWebjob.AddAzureStorageCoreServices();
                configWebjob.AddTimers();
            }).ConfigureLogging((hostContext, configLogging) =>
            {
                configLogging.AddConsole();
                configLogging.SetMinimumLevel(LogLevel.Trace);
                if (!hostContext.HostingEnvironment.IsDevelopment())
                {
                    configLogging.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Trace);
                    configLogging.AddApplicationInsights();
                }
            }).ConfigureServices((hostContext, services) =>
            {

                var serviceProvider = services.BuildServiceProvider();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                services.AddCVDataEngine(hostContext.Configuration);
                services.AddCVDataEngineDataFetching(hostContext.Configuration);
                services.AddCVDataEnginePlatformAuthentication(hostContext.Configuration);

                var serviceBusConnectionString = hostContext.Configuration.GetConnectionString("ServiceBus");

                services.AutoRegisterHandlersFromAssemblyOf<DataFetchCompleteHandler>();
                services.AutoRegisterHandlersFromAssemblyOf<DataFetchCompleteMessageHandler>(); //Gigplatform data update handler.

                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                services.AddRebus(c =>
                    c
                        .Transport(t =>
                            t.UseAzureServiceBus(
                                serviceBusConnectionString,
                                "platformdatafetcher.input"))
                        .Options(o =>
                        {
                            o.SimpleRetryStrategy(errorQueueAddress: "platformdatafetcher.error",
                                secondLevelRetriesEnabled: true);
                            o.SetNumberOfWorkers(1);
                            o.SetMaxParallelism(1);
                        })
                        .Logging(l => l.MicrosoftExtensionsLogging(loggerFactory))
                        .Routing(r => r.TypeBased()
                            .Map<PlatformConnectionUpdateNotificationMessage>("platformconnection.update")).Serialization(
                            s =>
                            {
                                var jsonSettings = new JsonSerializerSettings
                                {
                                    TypeNameHandling = TypeNameHandling.Auto,
                                    ContractResolver = new PrivateResolver()
                                };

                                s.UseNewtonsoftJson(jsonSettings);
                            })

                );

                if (hostContext.HostingEnvironment.IsDevelopment())
                {
                    services.AddDistributedMemoryCache();
                }
                else
                {
                    services.AddDistributedRedisCache(a =>
                    {
                        a.Configuration = hostContext.Configuration.GetConnectionString("Redis");
                        a.InstanceName = "master";
                    });
                }
                

                var ravenDbSection = hostContext.Configuration.GetSection("RavenDb");
                var urls = new List<string>();
                ravenDbSection.GetSection("Urls").Bind(urls);
                var databaseName = ravenDbSection.GetValue<string>("DatabaseName");
                var certThumbprint = ravenDbSection.GetValue<string>("CertificateThumbprint");

                logger.LogInformation($"Will use the following database name: '{databaseName}'");
                logger.LogInformation($"Will use the following database urls: {string.Join(", ", urls)}");

                DocumentStoreHolder.Logger = logger;
                DocumentStoreHolder.Urls = urls.ToArray();
                DocumentStoreHolder.DatabaseName = databaseName;
                DocumentStoreHolder.CertificateThumbprint = certThumbprint;
                DocumentStoreHolder.IsDevelopment = hostContext.HostingEnvironment.IsDevelopment();
                DocumentStoreHolder.TypeInAssemblyContainingIndexesToCreate =
                    typeof(Users_ByPlatformConnectionPossiblyRipeForDataFetch);
                services.AddSingleton<IDocumentStore>(DocumentStoreHolder.Store);
            });

            var host = builder.Build();

            using (host)
            {
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Will start Rebus");

                host.Services.UseRebus();

                logger.LogInformation("Setting up handler for unhandled exceptions.");
                var currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += (sender, eventArgs) =>
                {
                    var exception = (Exception)eventArgs.ExceptionObject;
                    logger.LogError(exception, "Got unhandled exception");
                };

                logger.LogInformation("Starting host.");
                host.Run();
            }
        }
    }

    public class PrivateResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member
            , MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);

            if (!prop.Writable)
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    var hasPrivateSetter = property.GetSetMethod(true) != null;
                    prop.Writable = hasPrivateSetter;
                }
            }

            return prop;
        }
    }

}
