using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers.Admin
{
    /// <summary>
    /// Admin methods for creating and manipulating apps.
    /// </summary>
    [Route("api/[controller]/admin")]
    [ApiController]
    public class AppAdminController : ControllerBase
    {
        private readonly IAppManager _appManager;
        private readonly IDocumentStore _documentStore;
        private readonly Options _options;

        public AppAdminController(IAppManager appManager, IDocumentStore documentStore, IOptions<Options> options)
        {
            _appManager = appManager;
            _documentStore = documentStore;
            _options = options.Value;
        }

        /// <summary>
        /// Get info for an existing app.
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("{applicationId}")]
        public async Task<ActionResult<AppInfoViewModel>> GetAppInfo([FromHeader(Name = "admin-key")] Guid adminKey,
            string applicationId, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();

            var (app, auth0App) =
                await _appManager.GetAppInfoFromApplicationId(applicationId, session, cancellationToken);

            return new AppInfoViewModel(app.Name, app.NotificationEndpoint,
                app.EmailVerificationNotificationEndpoint, auth0App.Callbacks.FirstOrDefault(),
                app.SecretKey, app.ApplicationId);
        }

        /// <summary>
        /// Create a new application.
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="model">The app creation data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<AppInfoViewModel>> CreateApp([FromHeader(Name = "admin-key")] Guid adminKey,
            [FromBody] AppCreateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            var (createdApp, createdAuth0App) = await _appManager.CreateApp(model.Name, model.NotificationEndpointUrl,
                model.EmailVerificationNotificationEndpointUrl, model.AuthCallbackUrl, true, session, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            return new AppInfoViewModel(createdApp.Name, createdApp.NotificationEndpoint,
                createdApp.EmailVerificationNotificationEndpoint, createdAuth0App.Callbacks?.FirstOrDefault(),
                createdApp.SecretKey, createdApp.ApplicationId);
        }

        /// <summary>
        /// Set notification endpoint url
        /// </summary>
        /// <param name="adminKey"></param>
        /// <param name="applicationId"></param>
        /// <param name="model"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/set-notification-endpoint-url")]
        public async Task<IActionResult> SetNotificationEndpointUrl([FromHeader(Name = "admin-key")] Guid adminKey,
            string applicationId, [FromBody] AppEndpointUpdateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetNotificationEndpointUrl(applicationId, model.Url, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpPatch("{applicationId}/set-email-verification-notification-endpoint-url")]
        public async Task<IActionResult> SetEmailVerificationNotificationEndpointUrl(
            [FromHeader(Name = "admin-key")] Guid adminKey, string applicationId, 
            [FromBody] AppEndpointUpdateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetEmailVerificationNotificationEndpointUrl(applicationId, model.Url, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpPatch("{applicationId}/set-auth-callback-url")]
        public async Task<IActionResult> SetAuthCallbackUrl([FromHeader(Name = "admin-key")] Guid adminKey,
            string applicationId, [FromBody] AppEndpointUpdateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetCallbackUrl(applicationId, model.Url, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpPatch("{applicationId}/set-name")]
        public async Task<IActionResult> SetName([FromHeader(Name = "admin-key")] Guid adminKey,
            string applicationId, [FromBody] AppNameUpdateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetName(applicationId, model.Name, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpPatch("{applicationId}/rotate-secret")]
        public async Task<IActionResult> RotateSecret([FromHeader(Name = "admin-key")] Guid adminKey,
            string applicationId, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.RotateSecret(applicationId, session, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok();
        }
    }

    public class AppInfoViewModel
    {
        public AppInfoViewModel(string name, string notificationEndpointUrl,
            string emailVerificationNotificationEndpointUrl, string authCallbackUrl, string secretKey,
            string applicationId)
        {
            Name = name;
            NotificationEndpointUrl = notificationEndpointUrl;
            EmailVerificationNotificationEndpointUrl = emailVerificationNotificationEndpointUrl;
            AuthCallbackUrl = authCallbackUrl;
            SecretKey = secretKey;
            ApplicationId = applicationId;
        }

        public string Name { get; private set; }
        public string NotificationEndpointUrl { get; private set; }
        public string EmailVerificationNotificationEndpointUrl { get; private set; }
        public string AuthCallbackUrl { get; private set; }
        public string SecretKey { get; private set; }
        public string ApplicationId { get; private set; }
    }

    public class AppCreateModel
    {
        [Required]
        public string Name { get; set; }
        public string NotificationEndpointUrl { get; set; }
        public string EmailVerificationNotificationEndpointUrl { get; set; }
        public string AuthCallbackUrl { get; set; }
    }

    public class AppEndpointUpdateModel
    {
        public string Url { get; set; }
    }

    public class AppNameUpdateModel
    {
        [Required]
        public string Name { get; set; }
    }
}