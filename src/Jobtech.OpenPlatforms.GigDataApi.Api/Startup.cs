using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Common.RavenDB;
using Jobtech.OpenPlatforms.GigDataApi.Engine.IoC;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;
using Swashbuckle.AspNetCore.Swagger;

namespace Jobtech.OpenPlatforms.GigDataApi.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment env)
        {
            Configuration = configuration;
            HostingEnvironment = env;
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment HostingEnvironment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            //services.AddLogging(configure => { configure.AddConsole(); });

            var auth0Section = Configuration.GetSection("Auth0");

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.Authority = auth0Section.GetValue<string>("TenantDomain");
                options.Audience = "cvdata.se";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
                };
            });

            var serviceBusConnectionString = Configuration.GetConnectionString("ServiceBus");

            services.AddRebus(c =>
                c.Transport(t =>
                        t.UseAzureServiceBusAsOneWayClient(
                            serviceBusConnectionString))
                    .Routing(r => r.TypeBased()
                        .Map<EmailVerificationNotificationMessage>("emailverification.update")
                        .Map<PlatformConnectionUpdateNotificationMessage>("platformconnection.update")
                    ));

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info
                {
                    Title = "GigData Api", 
                    Version = "v1",
                    Contact = new Contact
                    {
                        Email = "bjorn@roombler.com",
                        Name = "Björn Milton"
                    },
                    Description = "The GigData Api is intended to be used by parties that in different ways wants to access a users gig data with the consent of the user."
                });
                c.DescribeAllEnumsAsStrings();

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            services.AddCors();

            services.AddCVDataEngine(Configuration);
            services.AddCVDataEngineAuthentication(Configuration);
            services.AddCVDataEnginePlatformAuthentication(Configuration);


            services.AddLogging(loggingBuilder =>
            {
                //loggingBuilder.AddConsole();
                loggingBuilder.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Trace);
                loggingBuilder.AddApplicationInsights("46cfb5c1-40f3-4b50-b234-159a06cc7aab");
            });
            services.AddApplicationInsightsTelemetry("46cfb5c1-40f3-4b50-b234-159a06cc7aab");

            var ravenDbSection = Configuration.GetSection("RavenDb");
            var urls = new List<string>();
            ravenDbSection.GetSection("Urls").Bind(urls);
            var databaseName = ravenDbSection.GetValue<string>("DatabaseName");

            DocumentStoreHolder.Urls = urls.ToArray();
            DocumentStoreHolder.DatabaseName = databaseName;
            DocumentStoreHolder.IsDevelopment = HostingEnvironment.IsDevelopment();
            services.AddSingleton<IDocumentStore>(DocumentStoreHolder.Store);

            //Setup options for the app
            var configuredPlatformExternalIdsSection = Configuration.GetSection("PlatformExternalIds");
            var configuredPlatformExternalIds = new Dictionary<string, Guid>();
            configuredPlatformExternalIdsSection.Bind(configuredPlatformExternalIds);

            var configuredAdminKeysSection = Configuration.GetSection("AdminKeys");
            var configuredAdminKeys = new List<Guid>();
            configuredAdminKeysSection.Bind(configuredAdminKeys);

            services.Configure<Options>(options =>
            {
                foreach (var configuredPlatformExternalId in configuredPlatformExternalIds)
                {
                    options.PlatformExternalIds.Add(configuredPlatformExternalId.Key, configuredPlatformExternalId.Value);
                }

                foreach (var configuredAdminKey in configuredAdminKeys)
                {
                    options.AdminKeys.Add(configuredAdminKey);
                }
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
                //use https redirection only in production right now, since ngrok-tunneling does not work with https right now.
                app.UseHttpsRedirection();
            }

            app.UseCors(options => {
                options.AllowAnyOrigin();
                options.AllowAnyHeader();
                options.AllowAnyMethod();
            });

            app.UseApiExceptionHandler();

            app.UseAuthentication();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "GigData Api V1");
                c.RoutePrefix = string.Empty;
            });

            app.UseRebus();

            app.UseMvc();
        }
    }

    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleException(context, ex);
            }
        }

        private async Task HandleException(HttpContext context, Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception from Exception Handler");

            var code = HttpStatusCode.InternalServerError; // 500 if unexpected

            //if (ex is MyNotFoundException) code = HttpStatusCode.NotFound;
            //else if (ex is MyUnauthorizedException) code = HttpStatusCode.Unauthorized;
            //else if (ex is MyException) code = HttpStatusCode.BadRequest;

            var result = JsonConvert.SerializeObject(new { error = exception.Message });
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)code;
            await context.Response.WriteAsync(result);
        }
    }

    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }

    public class Options
    {
        public Options()
        {
            PlatformExternalIds = new Dictionary<string, Guid>();
            AdminKeys = new List<Guid>();
        }

        public IDictionary<string, Guid> PlatformExternalIds { get; private set; }
        public IList<Guid> AdminKeys { get; private set; }
    }
}
