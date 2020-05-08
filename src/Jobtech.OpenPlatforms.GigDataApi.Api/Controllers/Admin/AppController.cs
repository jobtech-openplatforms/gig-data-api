using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
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
    public class AppController : AdminControllerBase
    {
        private readonly IAppManager _appManager;

        private readonly IDocumentStore _documentStore;

        public AppController(IAppManager appManager,
            IDocumentStore documentStore, IOptions<Options> options) :
            base(options)
        {
            _appManager = appManager;
            _documentStore = documentStore;
        }

        /// <summary>
        /// Get info for an existing app.
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("{applicationId}")]
        [Produces("application/json")]
        public async Task<ActionResult<AppInfoViewModel>> GetAppInfo([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();

            var app =
                await _appManager.GetAppFromApplicationId(applicationId, session, cancellationToken);

            return new AppInfoViewModel(app.Name, app.DataUpdateCallbackUrl,
                app.AuthorizationCallbackUrl,
                app.SecretKey, app.ExternalId.ToString(), app.WebsiteUrl, app.DefaultPlatformDataClaim);
        }

        /// <summary>
        /// Create a new application.
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="model">The app creation data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost]
        [Produces("application/json")]
        public async Task<ActionResult<AppInfoViewModel>> CreateApp([FromHeader(Name = "admin-key")] Guid adminKey,
            [FromBody] AppCreateModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var createdApp = await _appManager.CreateApp(model.Name, model.DataUpdateCallbackUrl,
                model.AuthorizationCallbackUrl, model.Description, model.LogoUrl,
                model.WebsiteUrl, model.DefaultPlatformDataClaim ?? PlatformDataClaim.Aggregated, session, true,
                cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            return new AppInfoViewModel(createdApp.Name, createdApp.DataUpdateCallbackUrl,
                createdApp.AuthorizationCallbackUrl,
                createdApp.SecretKey, createdApp.ExternalId.ToString(), createdApp.WebsiteUrl, createdApp.DefaultPlatformDataClaim);
        }

        /// <summary>
        /// Set the data update callback url
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="model">The endpoint url data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/set-data-update-callback-url")]
        public async Task<IActionResult> SetDataUpdateCallbackUrl([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, [FromBody] AppEndpointUpdateModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetDataUpdateCallbackUrl(applicationId, model.Url, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok("Data update callback url updated");
        }

        /// <summary>
        /// Set the authorization callback url
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="model">The endpoint url data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/set-authorization-callback-url")]
        public async Task<IActionResult> SetAuthCallbackUrl([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, [FromBody] AppEndpointUpdateModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetAuthorizationCallbackUrl(applicationId, model.Url, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok("Authorization callback url updated");
        }

        /// <summary>
        /// Set the name
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="model">The name data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/set-name")]
        public async Task<IActionResult> SetName([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, [FromBody] AppNameUpdateModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetName(applicationId, model.Name, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok("Name updated");
        }

        /// <summary>
        /// Set the description
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="model">The description data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/set-description")]
        public async Task<IActionResult> SetDescription([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, [FromBody] AppDescriptionUpdateModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetDescription(applicationId, model.Description, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok("Description updated");
        }

        /// <summary>
        /// Set the logo url
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="model">The description data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/set-logourl")]
        public async Task<IActionResult> SetLogoUrl([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, [FromBody] AppLogoUrlUpdateModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetLogoUrl(applicationId, model.LogoUrl, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok("Logo url updated");
        }

        /// <summary>
        /// Set the website url
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="model">The website url data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/set-websiteurl")]
        public async Task<IActionResult> SetWebsiteUrl([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, [FromBody] AppWebsiteUrlUpdateModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetWebsiteUrl(applicationId, model.WebsiteUrl, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok("Website url updated");
        }

        /// <summary>
        /// Set the default platform data claim
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="model">The default platform data claim data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/set-default-platform-data-claim")]
        public async Task<IActionResult> SetDefaultPlatformDataClaim([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, [FromBody] AppDefaultPlatformDataClaimUpdateModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetDefaultPlatformDataClaim(applicationId, model.DefaultPlatformDataClaim, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok("Default platform data claim updated");
        }

        /// <summary>
        /// Rotate the app secret
        /// </summary>
        /// <param name="adminKey"></param>
        /// <param name="applicationId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The new secret key</returns>
        [HttpPatch("{applicationId}/rotate-secret")]
        [Produces("application/json")]
        public async Task<ActionResult<AppSecretViewModel>> RotateSecret([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var newSecret = await _appManager.RotateSecret(applicationId, session, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return new AppSecretViewModel(newSecret);
        }

        /// <summary>
        /// Set application to active.
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/activate")]
        public async Task<ActionResult> ActivateApplication([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var app = await _appManager.GetAppFromApplicationId(applicationId, session, cancellationToken);
            app.IsInactive = false;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Application set to active");
        }

        /// <summary>
        /// Set application to inactive
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="applicationId">The application id</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPatch("{applicationId}/deactivate")]
        public async Task<ActionResult> DeactivateApplication([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid applicationId, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var app = await _appManager.GetAppFromApplicationId(applicationId, session, cancellationToken);
            app.IsInactive = true;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Application set to inactive");
        }
    }

    public class AppInfoViewModel
    {
        public AppInfoViewModel(string name, string dataUpdateCallbackUrl,
            string authorizationCallbackUrl, string secretKey,
            string applicationId, string websiteUrl, PlatformDataClaim defaultPlatformDataClaim)
        {
            Name = name;
            DataUpdateCallbackUrl = dataUpdateCallbackUrl;
            AuthorizationCallbackUrl = authorizationCallbackUrl;
            SecretKey = secretKey;
            ApplicationId = applicationId;
            WebsiteUrl = websiteUrl;
            DefaultPlatformDataClaim = defaultPlatformDataClaim;
        }

        public string Name { get; private set; }
        public string DataUpdateCallbackUrl { get; private set; }
        public string AuthorizationCallbackUrl { get; private set; }
        public string SecretKey { get; private set; }
        public string ApplicationId { get; private set; }
        public string WebsiteUrl { get; private set; }
        public PlatformDataClaim DefaultPlatformDataClaim { get; private set; }
    }

    public class AppSecretViewModel
    {
        public AppSecretViewModel(string secretKey)
        {
            SecretKey = secretKey;
        }

        public string SecretKey { get; private set; }
    }

    public class AppCreateModel
    {
        [Required, MaxLength(1024)] public string Name { get; set; }
        [MaxLength(2048)] public string DataUpdateCallbackUrl { get; set; }
        [MaxLength(2048)] public string AuthorizationCallbackUrl { get; set; }
        [MaxLength(1024)] public string Description { get; set; }
        [MaxLength(2048)] public string LogoUrl { get; set; }
        [MaxLength(2048)] public string WebsiteUrl { get; set; }
        /// <summary>
        /// If not set the app will by default have platform data claim Aggregated.
        /// </summary>
        public PlatformDataClaim? DefaultPlatformDataClaim { get; set; }
    }

    public class AppEndpointUpdateModel
    {
        [MaxLength(2048)] public string Url { get; set; }
    }

    public class AppNameUpdateModel
    {
        [Required, MaxLength(1024)] public string Name { get; set; }
    }

    public class AppDescriptionUpdateModel
    {
        [MaxLength(1024)] public string Description { get; set; }
    }

    public class AppLogoUrlUpdateModel
    {
        [MaxLength(2048)] public string LogoUrl { get; set; }
    }

    public class AppWebsiteUrlUpdateModel
    {
        [MaxLength(2048)] public string WebsiteUrl { get; set; }
    }

    public class AppDefaultPlatformDataClaimUpdateModel
    {
        [Required] public PlatformDataClaim DefaultPlatformDataClaim { get; set; }
    }
}