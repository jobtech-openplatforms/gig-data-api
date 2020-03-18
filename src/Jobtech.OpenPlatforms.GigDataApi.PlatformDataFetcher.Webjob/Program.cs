using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Common.RavenDB;
using Jobtech.OpenPlatforms.GigDataApi.Engine.IoC;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Indexes;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Raven.Client.Documents;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using Rebus.ServiceProvider;
using Serilog;
using Serilog.Formatting.Elasticsearch;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob
{
    class Program
    {
        static async Task Main(string[] args)
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
                configApp.AddJsonFile("/app/secrets/appsettings.secrets.json", optional: true);
                configApp.AddJsonFile("appsettings.local.json", optional: true,
                    reloadOnChange: false); //load local settings

                configApp.AddEnvironmentVariables();
            }).ConfigureLogging((hostContext, configLogging) =>
            {
                var formatElastic = hostContext.Configuration.GetValue("FormatLogsInElasticFormat", false);

                var logConf = new LoggerConfiguration()
                    .ReadFrom.Configuration(hostContext.Configuration);

                if (formatElastic)
                {
                    var logFormatter = new ExceptionAsObjectJsonFormatter(renderMessage: true);
                    logConf.WriteTo.Console(logFormatter);
                }
                else
                {
                    logConf.WriteTo.Console();
                }

                Log.Logger = logConf.CreateLogger();

                configLogging.AddSerilog(dispose: true);
            }).ConfigureServices((hostContext, services) =>
            {
                var serviceProvider = services.BuildServiceProvider();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                services.AddCVDataEngine(hostContext.Configuration);
                services.AddCVDataEngineDataFetching(hostContext.Configuration);
                services.AddCVDataEnginePlatformAuthentication(hostContext.Configuration);

                services.AutoRegisterHandlersFromAssemblyOf<DataFetchCompleteHandler>();
                services.AutoRegisterHandlersFromAssemblyOf<DataFetchCompleteMessageHandler>(); //Gigplatform data update handler.

                var rebusSection = hostContext.Configuration.GetSection("Rebus");
                var inputQueueName = rebusSection.GetValue<string>("InputQueueName");
                var errorQueueName = rebusSection.GetValue<string>("ErrorQueueName");
                services.Configure<RebusConfiguration>(c =>
                {
                    c.InputQueueName = inputQueueName;
                    c.ErrorQueueName = errorQueueName;
                });

                var serviceBusConnectionString = hostContext.Configuration.GetConnectionString("ServiceBus");
                services.AddRebus(c =>
                    c
                        .Transport(t =>
                            t.UseAzureServiceBus(
                                serviceBusConnectionString,
                                inputQueueName))
                        .Options(o =>
                        {
                            o.SimpleRetryStrategy(errorQueueAddress: errorQueueName,
                                secondLevelRetriesEnabled: true);
                            o.SetNumberOfWorkers(1);
                            o.SetMaxParallelism(1);
                        })
                        .Logging(l => l.Serilog())
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
                var certPwd = ravenDbSection.GetValue<string>("CertPwd");
                var certPath = ravenDbSection.GetValue<string>("CertPath");
                var keyPath = ravenDbSection.GetValue<string>("KeyPath");

                logger.LogInformation($"Will use the following database name: '{databaseName}'");
                logger.LogInformation($"Will use the following database urls: {string.Join(", ", urls)}");

                DocumentStoreHolder.Logger = logger;
                DocumentStoreHolder.Urls = urls.ToArray();
                DocumentStoreHolder.DatabaseName = databaseName;
                DocumentStoreHolder.CertPwd = certPwd;
                DocumentStoreHolder.CertPath = certPath;
                DocumentStoreHolder.KeyPath = keyPath;
                DocumentStoreHolder.TypeInAssemblyContainingIndexesToCreate =
                    typeof(Users_ByPlatformConnectionPossiblyRipeForDataFetch);
                services.AddSingleton<IDocumentStore>(DocumentStoreHolder.Store);

            });

            var host = builder.Build();

            using (host)
            {
                var logger = host.Services.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("Setting up handler for unhandled exceptions.");
                var currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += (sender, eventArgs) =>
                {
                    var exception = (Exception)eventArgs.ExceptionObject;
                    logger.LogError(exception, "Got unhandled exception");
                };

                logger.LogInformation("Will start Rebus");
                host.Services.UseRebus();

                var bus = host.Services.GetRequiredService<IBus>();
                var message = new PlatformDataFetcherTriggerMessage();
                await bus.SendLocal(message);

                logger.LogInformation("Starting host.");
                await host.RunAsync();
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
