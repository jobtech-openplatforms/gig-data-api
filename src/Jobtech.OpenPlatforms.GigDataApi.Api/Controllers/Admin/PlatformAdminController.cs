using System;
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
    public class PlatformAdminController : AdminControllerBase
    {
        private readonly IPlatformManager _platformManager;
        private readonly IDocumentStore _documentStore;
        private readonly Options _options;

        public PlatformAdminController(IPlatformManager platformManager, IDocumentStore documentStore,
            IOptions<Options> options): base(options)
        {
            _platformManager = platformManager;
            _documentStore = documentStore;
            _options = options.Value;
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
        public async Task<ActionResult<PlatformInfoViewModel>> CreatePlatform(
            [FromHeader(Name = "admin-key")] Guid adminKey, [FromBody] CreatePlatformModel model,
            CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            var createdPlatform = await _platformManager.CreatePlatform(model.Name, model.AuthMechanism,
                PlatformIntegrationType.GigDataPlatformIntegration,
                new RatingInfo(model.MinRating, model.MaxRating, model.RatingSuccessLimit),
                3600, Guid.NewGuid(), model.Description, model.LogoUrl, session, true, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            return new PlatformInfoViewModel(createdPlatform.ExternalId, createdPlatform.Name,
                createdPlatform.Description, createdPlatform.LogoUrl, createdPlatform.IsInactive, createdPlatform.AuthenticationMechanism);
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
        public async Task<ActionResult<PlatformInfoViewModel>> GetPlatformInfo(
            [FromHeader(Name = "admin-key")] Guid adminKey, Guid platformId, CancellationToken cancellationToken)
        {
            //if (!_options.AdminKeys.Contains(adminKey))
            //{
            //    return Unauthorized();
            //}

            ValidateAdminKey(adminKey);

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            return new PlatformInfoViewModel(platform.ExternalId, platform.Name, platform.Description, platform.LogoUrl,
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
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

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
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            platform.IsInactive = true;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Platform set to inactive");
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
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

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
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            var platform = await _platformManager.GetPlatformByExternalId(platformId, session, cancellationToken);
            platform.Description = model.Description;

            await session.SaveChangesAsync(cancellationToken);

            return Ok("Platform logo url updated");
        }
    }

    public class PlatformInfoViewModel : PlatformViewModel
    {
        public PlatformInfoViewModel(Guid externalPlatformId, string name, string description, string logoUrl,
            bool isInactive, PlatformAuthenticationMechanism authMechanism) : base(externalPlatformId, name,
            description, logoUrl, authMechanism)
        {
            IsInactive = isInactive;
        }

        public bool IsInactive { get; set; }
    }
}