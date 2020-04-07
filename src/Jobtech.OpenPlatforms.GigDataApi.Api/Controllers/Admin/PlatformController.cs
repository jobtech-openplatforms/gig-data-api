using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers.Admin
{
    /// <summary>
    /// Admin methods for creating and manipulate platforms.
    /// </summary>
    [Route("api/[controller]/admin")]
    [ApiController]
    public class PlatformController : AdminControllerBase
    {
        private readonly IPlatformManager _platformManager;
        private readonly IDocumentStore _documentStore;

        public PlatformController(IPlatformManager platformManager, IDocumentStore documentStore,
            IOptions<Options> options) : base(options)
        {
            _platformManager = platformManager;
            _documentStore = documentStore;
        }


        /// <summary>
        /// Create a new platform
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="model">The data needed for creating a new platform</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost]
        [Produces("application/json")]
        public async Task<ActionResult<PlatformInfoViewModel>> CreatePlatform(
            [FromHeader(Name = "admin-key")] Guid adminKey, [FromBody] CreatePlatformModel model,
            CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var createdPlatform = await _platformManager.CreatePlatform(model.Name, model.AuthMechanism,
                PlatformIntegrationType.GigDataPlatformIntegration,
                new RatingInfo(model.MinRating, model.MaxRating, model.RatingSuccessLimit),
                3600, Guid.NewGuid(), model.Description, model.LogoUrl, model.WebsiteUrl, session, true,
                cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            return new PlatformInfoViewModel(createdPlatform.ExternalId, createdPlatform.Name,
                createdPlatform.Description, createdPlatform.LogoUrl, createdPlatform.WebsiteUrl,
                createdPlatform.IsInactive,
                createdPlatform.AuthenticationMechanism);
        }

        /// <summary>
        /// Get info about platform with the given platform id.
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="platformId">The platform id</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet("{platformId}")]
        [Produces("application/json")]
        public async Task<ActionResult<PlatformInfoViewModel>> GetPlatformInfo(
            [FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            return new PlatformInfoViewModel(platform.ExternalId, platform.Name, platform.Description, platform.LogoUrl,
                platform.WebsiteUrl,
                platform.IsInactive, platform.AuthenticationMechanism);
        }

        /// <summary>
        /// Set platform to active.
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="platformId">The platform id</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPatch("{platformId}/activate")]
        public async Task<ActionResult> ActivatePlatform([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid platformId, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            platform.IsInactive = false;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Platform set to active");
        }

        /// <summary>
        /// Set platform to inactive
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="platformId">The platform id</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPatch("{platformId}/deactivate")]
        public async Task<ActionResult> DeactivatePlatform([FromHeader(Name = "admin-key")] Guid adminKey,
            Guid platformId, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            platform.IsInactive = true;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Platform set to inactive");
        }

        /// <summary>
        /// Set the name for a platform
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="platformId">The platform id</param>
        /// <param name="model">Logo name data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPatch("{platformId}/set-name")]
        public async Task<ActionResult> SetName([FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId,
            [FromBody] SetNameModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            platform.Name = model.Name;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Platform name updated");
        }

        /// <summary>
        /// Set the logo url for a platform
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="platformId">The platform id</param>
        /// <param name="model">Logo url data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPatch("{platformId}/set-logourl")]
        public async Task<ActionResult> SetLogoUrl([FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId,
            [FromBody] SetLogoUrlModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            platform.LogoUrl = model.LogoUrl;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Platform logo url updated");
        }

        /// <summary>
        /// Set the description for a platform
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="platformId">The platform id</param>
        /// <param name="model">The description data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPatch("{platformId}/set-description")]
        public async Task<ActionResult> SetDescription([FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId,
            [FromBody] SetDescriptionModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            platform.Description = model.Description;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Platform logo url updated");
        }

        /// <summary>
        /// Set the website url for a platform
        /// </summary>
        /// <param name="adminKey">The admin key</param>
        /// <param name="platformId">The platform id</param>
        /// <param name="model">The website url data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPatch("{platformId}/set-websiteurl")]
        public async Task<ActionResult> SetWebsiteUrl([FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId,
            [FromBody] SetWebsiteUrlModel model, CancellationToken cancellationToken)
        {
            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            platform.WebsiteUrl = model.WebsiteUrl;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Platform website url updated");
        }
    }

    public class PlatformInfoViewModel : PlatformViewModel
    {
        public PlatformInfoViewModel(Guid externalPlatformId, string name, string description, string logoUrl,
            string websiteUrl,
            bool isInactive, PlatformAuthenticationMechanism authMechanism) : base(externalPlatformId, name,
            description, logoUrl, websiteUrl, authMechanism)
        {
            IsInactive = isInactive;
        }

        public bool IsInactive { get; set; }
    }

    public class CreatePlatformModel
    {
        [Required, MaxLength(1024)] public string Name { get; set; }

        [Required, JsonConverter(typeof(JsonStringEnumConverter))]
        public PlatformAuthenticationMechanism AuthMechanism { get; set; }

        [Required] public decimal MinRating { get; set; }
        [Required] public decimal MaxRating { get; set; }
        [Required] public decimal RatingSuccessLimit { get; set; }
        [MaxLength(1024)] public string Description { get; set; }
        [MaxLength(2048)] public string LogoUrl { get; set; }
        [MaxLength(2048)] public string WebsiteUrl { get; set; }
    }

    public class SetLogoUrlModel
    {
        [MaxLength(2048)] public string LogoUrl { get; set; }
    }

    public class SetNameModel
    {
        [Required, MaxLength(1024)] public string Name { get; set; }
    }

    public class SetDescriptionModel
    {
        [MaxLength(1024)] public string Description { get; set; }
    }

    public class SetWebsiteUrlModel
    {
        [MaxLength(2048)] public string WebsiteUrl { get; set; }
    }
}