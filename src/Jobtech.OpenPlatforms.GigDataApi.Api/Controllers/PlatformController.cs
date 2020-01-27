using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    /// <summary>
    /// Used for adding connections between users and supported platforms.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PlatformController : ControllerBase
    {
        private readonly IDocumentStore _documentStore;
        private readonly IPlatformManager _platformManager;
        private readonly IPlatformDataManager _platformDataManager;
        private readonly IAppManager _appManager;
        private readonly IAppNotificationManager _appNotificationManager;
        private readonly IPlatformConnectionManager _platformConnectionManager;
        private readonly IUserManager _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Options _options;
        private readonly ILogger<PlatformController> _logger;

        public PlatformController(IDocumentStore documentStore, IPlatformManager platformManager, IPlatformDataManager platformDataManager, 
            IAppManager appManager, IAppNotificationManager appNotificationManager, IPlatformConnectionManager platformConnectionManager, IUserManager userManager,
            IHttpContextAccessor httpContextAccessor, IOptions<Options> options, ILogger<PlatformController> logger)
        {
            _documentStore = documentStore;
            _platformManager = platformManager;
            _platformDataManager = platformDataManager;
            _appManager = appManager;
            _appNotificationManager = appNotificationManager;
            _platformConnectionManager = platformConnectionManager;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _options = options.Value;
            _logger = logger;
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = false)]
        [HttpPost("admin/create")]
        public async Task<ActionResult<PlatformViewModel>> CreatePlatform([FromHeader(Name = "admin-key")] Guid adminKey, [FromBody]CreatePlatformModel model)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using (var session = _documentStore.OpenAsyncSession())
            {
                var createdPlatform = await _platformManager.CreatePlatform(model.Name, model.AuthMechanism,
                    PlatformIntegrationType.GigDataPlatformIntegration, new RatingInfo(model.MinRating, model.MaxRating, model.RatingSuccessLimit), 
                    3600, Guid.NewGuid(), model.Description, model.LogoUrl, session, true);

                await session.SaveChangesAsync();

                return new PlatformViewModel(createdPlatform.ExternalId, createdPlatform.Name,
                    createdPlatform.Description, createdPlatform.LogoUrl, createdPlatform.AuthenticationMechanism);
            }
            
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = false)]
        [HttpPatch("{platformId}/admin/activate")]
        public async Task<ActionResult> ActivatePlatform([FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using (var session = _documentStore.OpenAsyncSession())
            {
                var platform = await _platformManager.GetPlatformByExternalId(platformId, session);
                platform.IsInactive = false;

                await session.SaveChangesAsync();

                return Ok("Platform set to active");
            }
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = false)]
        [HttpPatch("{platformId}/admin/inactivate")]
        public async Task<ActionResult> InactivatePlatform([FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using (var session = _documentStore.OpenAsyncSession())
            {
                var platform = await _platformManager.GetPlatformByExternalId(platformId, session);
                platform.IsInactive = true;

                await session.SaveChangesAsync();

                return Ok("Platform set to inactive");
            }
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = false)]
        [HttpPatch("{platformId}/admin/set-logourl")]
        public async Task<ActionResult> SetLogoUrl([FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId, [FromBody] SetLogoUrlModel model)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using (var session = _documentStore.OpenAsyncSession())
            {
                var platform = await _platformManager.GetPlatformByExternalId(platformId, session);
                platform.LogoUrl = model.LogoUrl;

                await session.SaveChangesAsync();

                return Ok("Platform logo url updated");
            }
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = false)]
        [HttpPatch("{platformId}/admin/set-description")]
        public async Task<ActionResult> SetDescription([FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId, [FromBody] SetDescriptionModel model)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using (var session = _documentStore.OpenAsyncSession())
            {
                var platform = await _platformManager.GetPlatformByExternalId(platformId, session);
                platform.Description = model.Description;

                await session.SaveChangesAsync();

                return Ok("Platform logo url updated");
            }
        }

        /// <summary>
        /// Get the connection status for a given platform.
        /// </summary>
        /// <remarks>
        /// Either a user has connected a platform or it has not. 
        /// </remarks>
        /// <param name="platformId">The id of the platform</param>
        /// <returns></returns>
        [HttpGet("{platformId}/connection-status")]
        public async Task<ActionResult<PlatformUserConnectionInfoViewModel>> GetPlatformUserConnectionStatus(Guid platformId)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using (var session = _documentStore.OpenAsyncSession())
            {
                var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session);
                await session.SaveChangesAsync();

                var platform = await _platformManager.GetPlatformByExternalId(platformId, session);

                var connectionForPlatform =
                    user.PlatformConnections.SingleOrDefault(pc => pc.ExternalPlatformId == platformId);
                if (connectionForPlatform != null)
                {
                    return new PlatformUserConnectionInfoViewModel(platform.ExternalId, platform.Name,
                        platform.Description, platform.LogoUrl, true, platform.AuthenticationMechanism);
                }

                return new PlatformUserConnectionInfoViewModel(platform.ExternalId, platform.Name, platform.Description,
                    platform.LogoUrl, false, platform.AuthenticationMechanism);
            }
        }

        [HttpGet("connection-status")]
        public async Task<ActionResult<IEnumerable<PlatformUserConnectionInfoViewModel>>> GetPlatformUserConnectionInfos()
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using (var session = _documentStore.OpenAsyncSession())
            {
                var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session);
                await session.SaveChangesAsync();

                var platforms = await _platformManager.GetAllPlatforms(session);

                var platformUserConnectionInfoViewModels = new List<PlatformUserConnectionInfoViewModel>();

                foreach (var platform in platforms)
                {
                    var isConnected = user.PlatformConnections.Any(pc => pc.PlatformId == platform.Id);
                    platformUserConnectionInfoViewModels.Add(new PlatformUserConnectionInfoViewModel(
                        platform.ExternalId, platform.Name, platform.Description, platform.LogoUrl, isConnected,
                        platform.AuthenticationMechanism));
                }

                return platformUserConnectionInfoViewModels;
            }
        }

        [HttpGet("connection-state")]
        public async Task<ActionResult<PlatformUserConnectionStateViewModel>> GetUserPlatformConnectionState()
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using (var session = _documentStore.OpenAsyncSession())
            {
                var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session);
                await session.SaveChangesAsync();

                var platformIds = user.PlatformConnections.Select(pc => pc.PlatformId).ToList();
                var platforms = (await _platformManager.GetPlatforms(platformIds, session)).Values.ToList();
                var appIds = user.PlatformConnections.Select(pc => pc.ConnectionInfo)
                    .SelectMany(ci => ci.NotificationInfos).Select(ni => ni.AppId).Distinct().ToList();
                var apps = (await _appManager.GetAppsFromIds(appIds, session)).ToList();

                return new PlatformUserConnectionStateViewModel(user.PlatformConnections, platforms, apps);
            }
        }

        [HttpPost("connection-state")]
        public async Task<ActionResult<PlatformUserConnectionStateViewModel>> UpdateUserPlatformConnectionState(
            [FromBody, Required] UserPlatformConnectionStateUpdateModel model)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using (var session = _documentStore.OpenAsyncSession())
            {
                var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session);
                await session.SaveChangesAsync();

                // update state
                foreach (var platformConnectionStateUpdate in model.PlatformConnectionStateUpdates)
                {
                    var platform =
                        await _platformManager.GetPlatformByExternalId(platformConnectionStateUpdate.PlatformId,
                            session);

                    var platformConnection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platform.Id);
                    if (platformConnection == null)
                    {
                        throw new PlatformConnectionDoesNotExistException("User does not have an existing connection to the platform");
                    }

                    if (platformConnectionStateUpdate.RemoveConnection)
                    {
                        if (platformConnectionStateUpdate.ConnectedApps != null)
                        {
                            throw new ArgumentException("Cannot both remove connection and update connected apps");
                        }

                        // remove connection and all saved data
                        await _platformDataManager.RemovePlatformDataForPlatform(user.Id, platform.Id, session);
                        var removedAppIds = user.PlatformConnections.Single(pc => pc.PlatformId == platform.Id)
                            .ConnectionInfo.NotificationInfos.Select(ni => ni.AppId).ToList();
                        user.PlatformConnections.Remove(platformConnection);
                        await _appNotificationManager.NotifyPlatformConnectionRemoved(user.Id, removedAppIds, platform.Id,
                            platform.ExternalId, platform.Name, session);
                    }
                    else
                    {

                        var existingApps = new List<App>();

                        foreach (var connectedApp in platformConnectionStateUpdate.ConnectedApps)
                        {
                            var correspondingApp = await _appManager.GetAppFromApplicationId(connectedApp, session); //existingApps.Single(a => a.ApplicationId == connectedApp);
                            if (existingApps.All(a => a.Id != correspondingApp.Id))
                            {
                                existingApps.Add(correspondingApp);
                            }
                            
                            if (platformConnection.ConnectionInfo.NotificationInfos.All(ni => ni.AppId != correspondingApp.Id))
                            {
                                throw new ArgumentException($"An existing connection between platform {platform.Name} and app {correspondingApp.Name} does not exist");
                            }
                        }

                        var updatedNotificationInfos = new List<NotificationInfo>();

                        var removedAppIds = new List<string>();

                        // update connected apps
                        foreach (var notificationInfo in platformConnection.ConnectionInfo.NotificationInfos)
                        {
                            var correspondingApp = existingApps.SingleOrDefault(a => a.Id == notificationInfo.AppId);
                            if (correspondingApp == null)
                            {
                                correspondingApp = await _appManager.GetAppFromId(notificationInfo.AppId, session);
                                existingApps.Add(correspondingApp);
                            }
                            if (platformConnectionStateUpdate.ConnectedApps.Any(applicationId => applicationId != correspondingApp.ApplicationId))
                            {
                                updatedNotificationInfos.Add(notificationInfo);
                            }
                            else
                            {
                                removedAppIds.Add(correspondingApp.Id);
                            }
                        }

                        platformConnection.ConnectionInfo.NotificationInfos = updatedNotificationInfos;

                        if (removedAppIds.Count > 0)
                        {
                            await _appNotificationManager.NotifyPlatformConnectionRemoved(user.Id, removedAppIds,
                                platform.Id, platform.ExternalId, platform.Name, session);
                        }

                    }

                    await session.SaveChangesAsync();
                }

                // gather and return new state
                var platformIds = user.PlatformConnections.Select(pc => pc.PlatformId).ToList();
                var platforms = (await _platformManager.GetPlatforms(platformIds, session)).Values.ToList();
                var appIds = user.PlatformConnections.Select(pc => pc.ConnectionInfo)
                    .SelectMany(ci => ci.NotificationInfos).Select(ni => ni.AppId).Distinct().ToList();
                var apps = (await _appManager.GetAppsFromIds(appIds, session)).ToList();

                return new PlatformUserConnectionStateViewModel(user.PlatformConnections, platforms, apps);
            }
        }

        [HttpPost("{platformId}/initiate-data-fetch")]
        public async Task<ActionResult> InitiateDataFetch(Guid platformId,
            [FromBody, Required] InitiateDataFetchModel model)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using (var session = _documentStore.OpenAsyncSession())
            {
                var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session);
                var platform = await _platformManager.GetPlatformByExternalId(platformId, session);
                await session.SaveChangesAsync();

                var platformConnection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platform.Id);

                if (platformConnection == null)
                {
                    throw new UserPlatformConnectionDoesNotExistException(platform.ExternalId, $"User with id {user.ExternalId} does not have access to platform");
                }

                //temp
                return Ok();

            }
        }

        [AllowAnonymous]
        [HttpGet("available")]
        public async Task<ActionResult<IEnumerable<PlatformViewModel>>> GetAllAvailablePlatforms()
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var platforms = await _platformManager.GetAllPlatforms(session);

                return platforms.Select(p => new PlatformViewModel(p.ExternalId, p.Name, p.Description, p.LogoUrl, p.AuthenticationMechanism)).ToList();
            }
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("freelancer/auth-complete")]
        public async Task<RedirectResult> FreelancerAuthComplete([FromQuery] string code, [FromQuery] string state)
        {
            string redirectUrl;
            var freelancerExternalId = _options.PlatformExternalIds["Freelancer"];
            using (var session = _documentStore.OpenAsyncSession())
            {
                redirectUrl = await _platformConnectionManager.CompleteConnectUserToOAuthPlatform(freelancerExternalId, code, state, session);
                await session.SaveChangesAsync();
            }

            return Redirect(redirectUrl);
        }
    }

    public class UserPlatformConnectionStateUpdateModel
    {
        [Required]
        public IEnumerable<UserPlatformConnectionStateModel> PlatformConnectionStateUpdates { get; set; }
    }

    public class UserPlatformConnectionStateModel
    {
        [Required]
        public Guid PlatformId { get; set; }
        public bool RemoveConnection { get; set; }
        public IEnumerable<string> ConnectedApps { get; set; }
    }

    public class CreatePlatformModel
    {
        [Required]
        public string Name { get; set; }
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public PlatformAuthenticationMechanism AuthMechanism { get; set; }
        [Required]
        public decimal MinRating { get; set; }
        [Required]
        public decimal MaxRating { get; set; }
        [Required]
        public decimal RatingSuccessLimit { get; set; }
        public string Description { get; set; }
        public string LogoUrl { get; set; }
    }

    public class InitiateDataFetchModel
    {
        public string ReportUri { get; set; }
    }

    public class SetLogoUrlModel
    {
        [Required, MaxLength(1024)]
        public string LogoUrl { get; set; }
    }

    public class SetDescriptionModel
    {
        [Required, MaxLength(1024)]
        public string Description { get; set; }
    }

    public class PlatformUserConnectionStateViewModel
    {
        public PlatformUserConnectionStateViewModel(IList<PlatformConnection> platformConnections, IList<Core.Entities.Platform> platforms,
            IList<App> apps)
        {
            Platforms = new List<PlatformUserConnectionInfoViewModel>();
            Apps = new List<AppUserConnectionViewModel>();

            foreach (var platformConnection in platformConnections)
            {
                var platform = platforms.Single(p => p.Id == platformConnection.PlatformId);
                Platforms.Add(new PlatformUserConnectionInfoViewModel(platform.ExternalId, platform.Name, platform.Description, platform.LogoUrl, true, platform.AuthenticationMechanism));

                foreach (var connectionInfoNotificationInfo in platformConnection.ConnectionInfo.NotificationInfos)
                {
                    var app = apps.Single(a => a.Id == connectionInfoNotificationInfo.AppId);
                    var appUserConnectionViewModel = Apps.SingleOrDefault(aucvm => aucvm.AppId == app.Id);
                    if (appUserConnectionViewModel == null)
                    {
                        Apps.Add(new AppUserConnectionViewModel(app.Name, app.ApplicationId, new List<Guid> {platform.ExternalId}));
                    }
                    else
                    {
                        appUserConnectionViewModel.ConnectedPlatforms.Add(platform.ExternalId);
                    }
                }
            }
        }

        public IList<PlatformUserConnectionInfoViewModel> Platforms { get; set; }
        public IList<AppUserConnectionViewModel> Apps { get; set; }
    }

    public class PlatformViewModel
    {

        public PlatformViewModel(Guid externalId, string name, string description, string logoUrl,
            PlatformAuthenticationMechanism authMechanism)
        {
            PlatformId = externalId;
            Name = name;
            Description = description;
            LogoUrl = logoUrl;
            AuthMechanism = authMechanism;
        }

        public Guid PlatformId { get; set; }
        public string Name { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public PlatformAuthenticationMechanism? AuthMechanism { get; set; }
        public string Description { get; set; }
        public string LogoUrl { get; set; }
    }

    public class PlatformUserConnectionInfoViewModel : PlatformViewModel
    {
        public PlatformUserConnectionInfoViewModel(Guid externalPlatformId, string name, string description, string logoUrl, bool isConnected,
            PlatformAuthenticationMechanism authMechanism) : base(externalPlatformId, name, description, logoUrl, authMechanism)
        {
            IsConnected = isConnected;
        }

        public bool IsConnected { get; set; }
    }

    public class AppUserConnectionViewModel
    {
        public AppUserConnectionViewModel(string name, string appId, IList<Guid> connectedPlatforms)
        {
            Name = name;
            AppId = appId;
            ConnectedPlatforms = connectedPlatforms;
        }

        public string Name { get; set; }
        public string AppId { get; set; }
        public IList<Guid> ConnectedPlatforms { get; set; }
    }
}