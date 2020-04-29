using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Api.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PlatformUserController : ControllerBase
    {
        private readonly IDocumentStore _documentStore;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPlatformConnectionManager _platformConnectionManager;
        private readonly IAppNotificationManager _appNotificationManager;
        private readonly IPlatformManager _platformManager;
        private readonly IUserManager _userManager;
        private readonly IAppManager _appManager;
        private readonly EmailVerificationConfiguration _emailVerificationConfiguration;

        public PlatformUserController(IDocumentStore documentStore, IHttpContextAccessor httpContextAccessor,
            IPlatformConnectionManager platformConnectionManager, IAppNotificationManager appNotificationManager,
            IPlatformManager platformManager, IUserManager userManager, IAppManager appManager, IOptions<EmailVerificationConfiguration> emailVerificationOptions)
        {
            _documentStore = documentStore;
            _httpContextAccessor = httpContextAccessor;
            _platformConnectionManager = platformConnectionManager;
            _appNotificationManager = appNotificationManager;
            _platformManager = platformManager;
            _userManager = userManager;
            _appManager = appManager;
            _emailVerificationConfiguration = emailVerificationOptions.Value;
        }

        [HttpPost("start-connect-user-to-oauth-platform")]
        [Produces("application/json")]
        public async Task<StartPlatformOauthConnectionResultViewModel> StartConnectUserToOauthPlatform(
            StartPlatformConnectionOauthModel model, CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var existingUser =
                await _userManager.GetUserByUniqueIdentifier(uniqueUserIdentifier, session, cancellationToken);

            var app = await _appManager.GetAppFromApplicationId(model.ApplicationId, session, cancellationToken);

            var authorizationResult = await _platformConnectionManager.StartConnectUserToOauthPlatform(model.PlatformId,
                existingUser, app, model.PlatformDataClaim,
                model.CallbackUri, session, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            return new StartPlatformOauthConnectionResultViewModel(authorizationResult.State,
                authorizationResult.OAuthAuthenticationUrl);
        }

        [HttpPost("request-platform-data-update-notification")]
        public async Task<IActionResult> RequestPlatformDataUpdateNotification(PlatformDataUpdateRequestModel model,
            CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var existingUser =
                await _userManager.GetUserByUniqueIdentifier(uniqueUserIdentifier, session, cancellationToken);

            var app = await _appManager.GetAppFromApplicationId(model.ApplicationId, session, cancellationToken);

            var platform = await _platformManager.GetPlatformByExternalId(model.PlatformId, session, cancellationToken);

            var platformConnectionInfo =
                _platformConnectionManager.GetPlatformConnectionInfo(existingUser, platform.Id);

            if (platformConnectionInfo.NotificationInfos.All(ni => ni.AppId != app.Id))
            {
                //app not registered for notification. Throw.
                throw new AppNotRegisteredForNotificationsException(
                    $"App with application id {app.ExternalId} is not registered for receiving notifications for platform with id {platform.Id} for user with id {existingUser.ExternalId}");
            }

            await _appNotificationManager.NotifyPlatformConnectionDataUpdate(existingUser.Id,
                new List<string> {app.Id}, platform.Id, session,
                cancellationToken);

            return new AcceptedResult();
        }

        [HttpPost("connect-user-to-email-platform")]
        [Produces("application/json")]
        public async Task<PlatformConnectionResultViewModel> ConnectUserToEmailPlatform(
            StartPlatformConnectionEmailModel model, CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var existingUser =
                await _userManager.GetUserByUniqueIdentifier(uniqueUserIdentifier, session, cancellationToken);

            var app = await _appManager.GetAppFromApplicationId(model.ApplicationId, session, cancellationToken);

            var result = await _platformConnectionManager.ConnectUserToEmailPlatform(model.PlatformId, existingUser,
                app,
                model.PlatformUserEmailAddress, _emailVerificationConfiguration.AcceptUrl,
                _emailVerificationConfiguration.DeclineUrl, model.PlatformDataClaim,
                session, cancellationToken: cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            return new PlatformConnectionResultViewModel(result.State);
        }
    }

    public abstract class StartPlatformConnectionModelBase
    {
        [Required] public Guid PlatformId { get; set; }
        [Required] public Guid ApplicationId { get; set; }
        public PlatformDataClaim? PlatformDataClaim { get; set; }
    }

    public class StartPlatformConnectionOauthModel : StartPlatformConnectionModelBase
    {
        [Required] public string CallbackUri { get; set; }
    }

    public class StartPlatformConnectionEmailModel : StartPlatformConnectionModelBase
    {
        public string PlatformUserEmailAddress { get; set; }
    }

    public class PlatformConnectionResultViewModel
    {
        public PlatformConnectionResultViewModel(PlatformConnectionState state)
        {
            State = state;
        }

        public PlatformConnectionState State { get; set; }
    }

    public class StartPlatformOauthConnectionResultViewModel : PlatformConnectionResultViewModel
    {
        public StartPlatformOauthConnectionResultViewModel(PlatformConnectionState state,
            string authorizationUri = null) : base(state)
        {
            AuthorizationUri = authorizationUri;
        }

        public string AuthorizationUri { get; set; }
    }

    public class PlatformDataUpdateRequestModel
    {
        [Required] public Guid ApplicationId { get; set; }
        [Required] public Guid PlatformId { get; set; }
    }
}