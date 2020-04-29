using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Api.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.Api.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Common.RavenDB;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Engine.IoC;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;
using Serilog;
using Serilog.Formatting.Elasticsearch;

namespace Jobtech.OpenPlatforms.GigDataApi.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            HostingEnvironment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment HostingEnvironment { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var formatElastic = Configuration.GetValue("FormatLogsInElasticFormat", false);

            // Logger configuration
            var logConf = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration);

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

            services.AddMvc().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

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
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (string.IsNullOrEmpty(context.Principal.Identity.Name))
                        {
                            return;
                        }

                        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                        var cacheEntry = cache.Get(context.Principal.Identity.Name);
                        if (cacheEntry != null)
                        {
                            return;
                        }

                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            // Keep in cache for this time, reset time if accessed.
                            .SetAbsoluteExpiration(TimeSpan.FromSeconds(60)).SetSize(1);
                        cache.Set(context.Principal.Identity.Name, true, cacheEntryOptions);

                        var userManager = context.HttpContext.RequestServices.GetRequiredService<IUserManager>();
                        var documentStore = context.HttpContext.RequestServices.GetRequiredService<IDocumentStore>();
                        using var session = documentStore.OpenAsyncSession();

                        var auth0Client = context.HttpContext.RequestServices.GetRequiredService<Auth0ManagementApiHttpClient>();
                        var userInfo = await auth0Client.GetUserProfile(context.Principal.Identity.Name);

                        var user = await userManager.GetOrCreateUserIfNotExists(context.Principal.Identity.Name, session);
                        user.Name = userInfo.Name;

                        var userEmailState = UserEmailState.Unverified;
                        if (userInfo.EmailVerified)
                        {
                            userEmailState = UserEmailState.Verified;
                        }

                        var existingUserEmail = user.UserEmails.SingleOrDefault(ue =>
                            ue.Email.ToLowerInvariant() == userInfo.Email.ToLowerInvariant());

                        if (existingUserEmail == null) //email does not exist at all
                        {
                            user.UserEmails.Add(new UserEmail(userInfo.Email.ToLowerInvariant(), userEmailState));
                        }
                        else if (existingUserEmail.UserEmailState != UserEmailState.Verified &&
                                 userEmailState == UserEmailState.Verified) //email has been verified through Auth0
                        {
                            existingUserEmail.SetEmailState(UserEmailState.Verified);
                        }

                        if (session.Advanced.HasChanges)
                        {
                            await session.SaveChangesAsync();
                        }

                    }
                };
            });

            var serviceBusConnectionString = Configuration.GetConnectionString("ServiceBus");

            services.AddRebus(c =>
                c.Transport(t =>
                        t.UseAzureServiceBusAsOneWayClient(
                            serviceBusConnectionString))
                    .Routing(r => r.TypeBased()
                        .Map<PlatformConnectionUpdateNotificationMessage>("platformdatafetcher.input")
                    ));

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            static IList<string> ApplyGroupName(ApiDescription apiDescription)
            {
                var tags = new List<string>();
                if (apiDescription.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
                {
                    var apiExplorerSettings = controllerActionDescriptor
                        .ControllerTypeInfo.GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), true)
                        .Cast<ApiExplorerSettingsAttribute>().FirstOrDefault();
                    if (apiExplorerSettings != null && !string.IsNullOrWhiteSpace(apiExplorerSettings.GroupName))
                    {
                        tags.Add(apiExplorerSettings.GroupName);
                    }
                    else
                    {
                        tags.Add(controllerActionDescriptor.ControllerName);
                    }
                }

                return tags;
            }

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "GigData Api", 
                    Version = "v1",
                    Contact = new OpenApiContact
                    {
                        Email = "bjorn@roombler.com",
                        Name = "Björn Milton"
                    },
                    Description = "The GigData Api is intended to be used by parties that in different ways wants to access a users gig data with the consent of the user."
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n " +
                                  $"Enter your token in the text input below.\r\n\r\n" +
                                  $"Example: \"12345abcdef\"",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Id = "Bearer",
                                Type = ReferenceType.SecurityScheme
                            }
                        },
                        new List<string>()
                    }
                });

                c.DescribeAllParametersInCamelCase();

                c.TagActionsBy(ApplyGroupName);
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            services.AddCors();

            services.AddCVDataEngine();
            services.AddCVDataEngineAuthentication(Configuration);
            services.AddCVDataEnginePlatformAuthentication(Configuration);

            var ravenDbSection = Configuration.GetSection("RavenDb");
            var urls = new List<string>();
            ravenDbSection.GetSection("Urls").Bind(urls);
            var databaseName = ravenDbSection.GetValue<string>("DatabaseName");
            var certPwd = ravenDbSection.GetValue<string>("CertPwd");
            var certPath = ravenDbSection.GetValue<string>("CertPath");
            var keyPath = ravenDbSection.GetValue<string>("KeyPath");

            DocumentStoreHolder.Urls = urls.ToArray();
            DocumentStoreHolder.DatabaseName = databaseName;
            DocumentStoreHolder.CertPwd = certPwd;
            DocumentStoreHolder.CertPath = certPath;
            DocumentStoreHolder.KeyPath = keyPath;
            services.AddSingleton(DocumentStoreHolder.Store);

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

            var emailVerificationSection = Configuration.GetSection("EmailVerification");
            services.Configure<EmailVerificationConfiguration>(c =>
            {
                c.AcceptUrl = emailVerificationSection.GetValue<string>("AcceptUrl");
                c.DeclineUrl = emailVerificationSection.GetValue<string>("DeclineUrl");
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseSerilogRequestLogging();

            app.UseCors(options => {
                options.AllowAnyOrigin();
                options.AllowAnyHeader();
                options.AllowAnyMethod();
            });

            app.UseRouting();

            app.UseApiExceptionHandler();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "GigData Api V1");
                c.RoutePrefix = string.Empty;
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
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

            var code = exception switch
            {
                AppDoesNotExistException _ => HttpStatusCode.NotFound,
                PlatformDoNotExistException _ => HttpStatusCode.NotFound,
                EmailPromptExpiredException _ => HttpStatusCode.BadRequest,
                EmailPromptDoesNotExistException _ => HttpStatusCode.NotFound,
                UnauthorizedAdminCallException _ => HttpStatusCode.Unauthorized,
                _ => HttpStatusCode.InternalServerError
            };

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
