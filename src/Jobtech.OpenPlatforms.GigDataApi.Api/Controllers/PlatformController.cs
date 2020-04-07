﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
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

        public PlatformController(IDocumentStore documentStore, IPlatformManager platformManager,
            IPlatformDataManager platformDataManager,
            IAppManager appManager, IAppNotificationManager appNotificationManager,
            IPlatformConnectionManager platformConnectionManager, IUserManager userManager,
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

        /// <summary>
        /// Get the connection status for a given platform.
        /// </summary>
        /// <remarks>
        /// Either a user has connected a platform or it has not. 
        /// </remarks>
        /// <param name="platformId">The id of the platform</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("{platformId}/connection-status")]
        [Produces("application/json")]
        public async Task<ActionResult<PlatformUserConnectionInfoViewModel>> GetPlatformUserConnectionStatus(
            Guid platformId, CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);

            var connectionForPlatform =
                user.PlatformConnections.SingleOrDefault(pc => pc.ExternalPlatformId == platformId);
            if (connectionForPlatform != null)
            {
                return new PlatformUserConnectionInfoViewModel(platform.ExternalId, platform.Name,
                    platform.Description, platform.LogoUrl, platform.WebsiteUrl, true,
                    platform.AuthenticationMechanism);
            }

            return new PlatformUserConnectionInfoViewModel(platform.ExternalId, platform.Name, platform.Description,
                platform.LogoUrl, platform.WebsiteUrl, false, platform.AuthenticationMechanism);
        }

        /// <summary>
        /// Get the connection status for all platforms available in the system.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("connection-status")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<PlatformUserConnectionInfoViewModel>>>
            GetPlatformUserConnectionInfos(CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            var platforms = await _platformManager.GetAllPlatforms(session, cancellationToken);

            var platformUserConnectionInfoViewModels = new List<PlatformUserConnectionInfoViewModel>();

            foreach (var platform in platforms)
            {
                var isConnected = user.PlatformConnections.Any(pc => pc.PlatformId == platform.Id);
                platformUserConnectionInfoViewModels.Add(new PlatformUserConnectionInfoViewModel(
                    platform.ExternalId, platform.Name, platform.Description, platform.LogoUrl, platform.WebsiteUrl,
                    isConnected,
                    platform.AuthenticationMechanism));
            }

            return platformUserConnectionInfoViewModels;
        }

        /// <summary>
        /// Get the connection state for connected platforms.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("connection-state")]
        [Produces("application/json")]
        public async Task<ActionResult<PlatformUserConnectionStateViewModel>> GetUserPlatformConnectionState(
            CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            var platformIds = user.PlatformConnections.Select(pc => pc.PlatformId).ToList();
            var platforms =
                (await _platformManager.GetPlatforms(platformIds, session, cancellationToken)).Values.ToList();
            var appIds = user.PlatformConnections.Select(pc => pc.ConnectionInfo)
                .SelectMany(ci => ci.NotificationInfos).Select(ni => ni.AppId).Distinct().ToList();
            var apps = (await _appManager.GetAppsFromIds(appIds, session, cancellationToken)).ToList();

            return new PlatformUserConnectionStateViewModel(user.PlatformConnections, platforms, apps);
        }

        /// <summary>
        /// Set the connection state for a given platform.
        /// </summary>
        /// <param name="model">The state update data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost("connection-state")]
        [Produces("application/json")]
        public async Task<ActionResult<PlatformUserConnectionStateViewModel>> UpdateUserPlatformConnectionState(
            [FromBody, Required] UserPlatformConnectionStateUpdateModel model, CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            // update state
            foreach (var platformConnectionStateUpdate in model.PlatformConnectionStateUpdates)
            {
                var platform =
                    await _platformManager.GetPlatformByExternalId(platformConnectionStateUpdate.PlatformId,
                        session, cancellationToken);

                var platformConnection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platform.Id);
                if (platformConnection == null)
                {
                    throw new PlatformConnectionDoesNotExistException(
                        "User does not have an existing connection to the platform");
                }

                if (platformConnectionStateUpdate.RemoveConnection)
                {
                    if (platformConnectionStateUpdate.ConnectedApps != null)
                    {
                        throw new ArgumentException("Cannot both remove connection and update connected apps");
                    }

                    // remove connection and all saved data
                    await _platformDataManager.RemovePlatformDataForPlatform(user.Id, platform.Id, session,
                        cancellationToken);
                    var removedAppIds = user.PlatformConnections.Single(pc => pc.PlatformId == platform.Id)
                        .ConnectionInfo.NotificationInfos.Select(ni => ni.AppId).ToList();
                    user.PlatformConnections.Remove(platformConnection);
                    await _appNotificationManager.NotifyPlatformConnectionRemoved(user.Id, removedAppIds, platform.Id,
                        session, cancellationToken);
                }
                else
                {

                    var existingApps = new List<App>();

                    foreach (var connectedApp in platformConnectionStateUpdate.ConnectedApps)
                    {
                        var correspondingApp =
                            await _appManager.GetAppFromApplicationId(connectedApp, session,
                                cancellationToken); //existingApps.Single(a => a.ApplicationId == connectedApp);
                        if (existingApps.All(a => a.Id != correspondingApp.Id))
                        {
                            existingApps.Add(correspondingApp);
                        }

                        if (platformConnection.ConnectionInfo.NotificationInfos.All(ni =>
                            ni.AppId != correspondingApp.Id))
                        {
                            throw new ArgumentException(
                                $"An existing connection between platform {platform.Name} and app {correspondingApp.Name} does not exist");
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
                            correspondingApp =
                                await _appManager.GetAppFromId(notificationInfo.AppId, session, cancellationToken);
                            existingApps.Add(correspondingApp);
                        }

                        if (platformConnectionStateUpdate.ConnectedApps.Any(applicationId =>
                            applicationId != correspondingApp.ApplicationId))
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
                            platform.Id, session, cancellationToken);
                    }

                }

                await session.SaveChangesAsync(cancellationToken);
            }

            // gather and return new state
            var platformIds = user.PlatformConnections.Select(pc => pc.PlatformId).ToList();
            var platforms =
                (await _platformManager.GetPlatforms(platformIds, session, cancellationToken)).Values.ToList();
            var appIds = user.PlatformConnections.Select(pc => pc.ConnectionInfo)
                .SelectMany(ci => ci.NotificationInfos).Select(ni => ni.AppId).Distinct().ToList();
            var apps = (await _appManager.GetAppsFromIds(appIds, session, cancellationToken)).ToList();

            return new PlatformUserConnectionStateViewModel(user.PlatformConnections, platforms, apps);
        }

        /// <summary>
        /// Initiate a data fetch for the platform with the given id
        /// </summary>
        /// <param name="platformId">The platform id</param>
        /// <param name="model"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost("{platformId}/initiate-data-fetch")]
        public async Task<IActionResult> InitiateDataFetch(Guid platformId,
            [FromBody, Required] InitiateDataFetchModel model, CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserIdentifier, session, cancellationToken);
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            var platformConnection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platform.Id);

            if (platformConnection == null)
            {
                throw new UserPlatformConnectionDoesNotExistException(platform.ExternalId,
                    $"User with id {user.ExternalId} does not have access to platform");
            }

            //temp
            return Ok();
        }

        /// <summary>
        /// Get info for the platform with the given id.
        /// </summary>
        /// <param name="platformId">The platform id.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("{platformId}")]
        [Produces("application/json")]
        public async Task<ActionResult<PlatformViewModel>> GetPlatformInfo(Guid platformId,
            CancellationToken cancellationToken)
        {
            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);

            return new PlatformViewModel(platform.ExternalId, platform.Name, platform.Description, platform.LogoUrl,
                platform.WebsiteUrl,
                platform.AuthenticationMechanism);
        }

        /// <summary>
        /// Get a list of all available platforms.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("available")]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<PlatformViewModel>>> GetAllAvailablePlatforms(
            CancellationToken cancellationToken)
        {
            using var session = _documentStore.OpenAsyncSession();
            var platforms = await _platformManager.GetAllPlatforms(session, cancellationToken);

            return platforms.Select(p =>
                    new PlatformViewModel(p.ExternalId, p.Name, p.Description, p.LogoUrl, p.WebsiteUrl,
                        p.AuthenticationMechanism))
                .ToList();
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("freelancer/auth-complete")]
        public async Task<RedirectResult> FreelancerAuthComplete([FromQuery] string code, [FromQuery] string state,
            CancellationToken cancellationToken)
        {
            var freelancerExternalId = _options.PlatformExternalIds["Freelancer"];

            using var session = _documentStore.OpenAsyncSession();
            var redirectUrl = await _platformConnectionManager.CompleteConnectUserToOAuthPlatform(freelancerExternalId,
                code, state, session, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            return Redirect(redirectUrl);
        }
    }

    public class UserPlatformConnectionStateUpdateModel
    {
        [Required] public IEnumerable<UserPlatformConnectionStateModel> PlatformConnectionStateUpdates { get; set; }
    }

    public class UserPlatformConnectionStateModel
    {
        [Required] public Guid PlatformId { get; set; }
        public bool RemoveConnection { get; set; }
        public IEnumerable<string> ConnectedApps { get; set; }
    }

    public class InitiateDataFetchModel
    {
        public string ReportUri { get; set; }
    }

    public class PlatformUserConnectionStateViewModel
    {
        public PlatformUserConnectionStateViewModel(IList<PlatformConnection> platformConnections,
            IList<Core.Entities.Platform> platforms,
            IList<App> apps)
        {
            Platforms = new List<PlatformUserConnectionInfoViewModel>();
            Apps = new List<AppUserConnectionViewModel>();

            foreach (var platformConnection in platformConnections)
            {
                var platform = platforms.Single(p => p.Id == platformConnection.PlatformId);
                Platforms.Add(new PlatformUserConnectionInfoViewModel(platform.ExternalId, platform.Name,
                    platform.Description, platform.LogoUrl, platform.WebsiteUrl, true,
                    platform.AuthenticationMechanism));

                foreach (var connectionInfoNotificationInfo in platformConnection.ConnectionInfo.NotificationInfos)
                {
                    var app = apps.Single(a => a.Id == connectionInfoNotificationInfo.AppId);
                    var appUserConnectionViewModel = Apps.SingleOrDefault(aucvm => aucvm.AppId == app.Id);
                    if (appUserConnectionViewModel == null)
                    {
                        Apps.Add(new AppUserConnectionViewModel(app.Name, app.ApplicationId,
                            new List<Guid> {platform.ExternalId}));
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

        public PlatformViewModel(Guid externalId, string name, string description, string logoUrl, string websiteUrl,
            PlatformAuthenticationMechanism authMechanism)
        {
            PlatformId = externalId;
            Name = name;
            Description = description;
            LogoUrl = logoUrl;
            AuthMechanism = authMechanism;
            WebsiteUrl = websiteUrl;
        }

        public Guid PlatformId { get; private set; }
        public string Name { get; private set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PlatformAuthenticationMechanism AuthMechanism { get; private set; }

        public string Description { get; private set; }
        public string LogoUrl { get; private set; }
        public string WebsiteUrl { get; private set; }
    }

    public class PlatformUserConnectionInfoViewModel : PlatformViewModel
    {
        public PlatformUserConnectionInfoViewModel(Guid externalPlatformId, string name, string description,
            string logoUrl, string websiteUrl, bool isConnected,
            PlatformAuthenticationMechanism authMechanism) : base(externalPlatformId, name, description, logoUrl,
            websiteUrl,
            authMechanism)
        {
            IsConnected = isConnected;
        }

        public bool IsConnected { get; private set; }
    }

    public class AppUserConnectionViewModel
    {
        public AppUserConnectionViewModel(string name, string appId, IList<Guid> connectedPlatforms)
        {
            Name = name;
            AppId = appId;
            ConnectedPlatforms = connectedPlatforms;
        }

        public string Name { get; private set; }
        public string AppId { get; private set; }
        public IList<Guid> ConnectedPlatforms { get; private set; }
    }
}