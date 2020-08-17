using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Common.RavenDB;
using Jobtech.OpenPlatforms.GigDataApi.Engine.IoC;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Indexes;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Rebus.Config;
using Rebus.Persistence.FileSystem;
using Rebus.Persistence.InMem;
using Rebus.RabbitMq;
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
        static async Task Main()
        {
            var builder = new HostBuilder();

            builder.ConfigureHostConfiguration(configHost =>
            {
                configHost.SetBasePath(Directory.GetCurrentDirectory());
                configHost.AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string>(HostDefaults.EnvironmentKey,
                            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")),
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

                services.AddGigDataApiEngine();
                services.AddGigDataApiEngineDataFetching(hostContext.Configuration);
                services.AddGigDataApiEnginePlatformAuthentication(hostContext.Configuration);

                services.AutoRegisterHandlersFromAssemblyOf<DataFetchCompleteHandler>();
                services.AutoRegisterHandlersFromAssemblyOf<DataFetchCompleteMessageHandler>(); //Gigplatform data update handler.

                var rebusSection = hostContext.Configuration.GetSection("Rebus");
                var inputQueueName = rebusSection.GetValue<string>("InputQueueName");
                var errorQueueName = rebusSection.GetValue<string>("ErrorQueueName");
                var timeoutsFilesystemFolder = rebusSection.GetValue<string>("TimeoutsFilesystemFolder");

                services.Configure<RebusConfiguration>(c =>
                {
                    c.InputQueueName = inputQueueName;
                    c.ErrorQueueName = errorQueueName;
                });

                var rabbitMqConnectionString = hostContext.Configuration.GetConnectionString("RabbitMq");
                var rabbitMqConnectionEndpoint = new ConnectionEndpoint
                {
                    ConnectionString = rabbitMqConnectionString
                };

                var jsonSerializerSettings = new JsonSerializerSettings
                {
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                    ContractResolver = new PrivateResolver()
                };

                services.AddRebus(c =>
                    c
                        .Transport(t =>
                            t.UseRabbitMq(new List<ConnectionEndpoint> {rabbitMqConnectionEndpoint}, 
                                inputQueueName))
                        .Timeouts(t => {
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                            {
                                //we can't use file system since it uses locking api on the underlying file stream which is not supported on OS X.
                                //See: https://github.com/dotnet/coreclr/commit/0daa63e9ed40323b6f248ded8959530ea94f19aa
                                t.StoreInMemory();
                            } else
                            {
                                t.UseFileSystem(timeoutsFilesystemFolder);
                            }
                            
                        })
                        .Options(o =>
                        {
                            o.SimpleRetryStrategy(errorQueueAddress: errorQueueName,
                                secondLevelRetriesEnabled: true);
                            o.SetNumberOfWorkers(1);
                            o.SetMaxParallelism(1);
                        })
                        .Logging(l => l.Serilog())
                        .Routing(r => r.TypeBased()
                            .Map<FetchDataForPlatformConnectionMessage>(inputQueueName)
                            .Map<PlatformConnectionUpdateNotificationMessage>(inputQueueName))
                        .Serialization(s => s.UseNewtonsoftJson(jsonSerializerSettings))

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
                services.AddSingleton(DocumentStoreHolder.Store);

                services.AddHostedService<TimedDataFetcherTriggerTask>();

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
