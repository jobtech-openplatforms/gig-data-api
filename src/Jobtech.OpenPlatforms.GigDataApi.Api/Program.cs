using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Rebus.ServiceProvider;
using Serilog;

namespace Jobtech.OpenPlatforms.GigDataApi.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var webHost = CreateWebHostBuilder(args).ConfigureLogging(configure =>
            {
                //configure.AddConsole();
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
            }).Build();

            webHost.Services.UseRebus();

            await webHost.RunAsync();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>().UseSerilog();
    }
}
