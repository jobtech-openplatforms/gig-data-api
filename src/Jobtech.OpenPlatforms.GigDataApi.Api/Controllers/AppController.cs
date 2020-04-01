using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    //[ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/[controller]")]
    [ApiController]
    public class AppController : ControllerBase
    {
        private readonly IAppManager _appManager;
        private readonly IDocumentStore _documentStore;
        private readonly Options _options;

        public AppController(IAppManager appManager, IDocumentStore documentStore, IOptions<Options> options)
        {
            _appManager = appManager;
            _documentStore = documentStore;
            _options = options.Value;
        }

        [HttpPost("admin/create")]
        public async Task<ActionResult<AppCreateResult>> CreateApp([FromHeader(Name = "admin-key")] Guid adminKey,
            [FromBody] AppCreateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            var createdApp = await _appManager.CreateApp(model.Name, model.NotificationEndpointUrl,
                model.EmailVerificationNotificationEndpointUrl, model.AuthCallbackUrl, session, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            return new AppCreateResult
            {
                SecretKey = createdApp.SecretKey,
                ApplicationId = createdApp.ApplicationId
            };
        }

        [HttpPut("admin/set-notification-endpoint-url")]
        public async Task<IActionResult> SetNotificationEndpointUrl([FromHeader(Name = "admin-key")] Guid adminKey, [FromBody] AppEndpointUpdateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetNotificationEndpointUrl(model.ApplicationId, model.Url, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpPut("admin/set-email-verification-notification-endpoint-url")]
        public async Task<IActionResult> SetEmailVerificationNotificationEndpointUrl([FromHeader(Name = "admin-key")] Guid adminKey, [FromBody] AppEndpointUpdateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetEmailVerificationNotificationEndpointUrl(model.ApplicationId, model.Url, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpPut("admin/set-auth-callback-url")]
        public async Task<IActionResult> SetAuthCallbackUrl([FromHeader(Name = "admin-key")] Guid adminKey, [FromBody] AppEndpointUpdateModel model, CancellationToken cancellationToken)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using var session = _documentStore.OpenAsyncSession();
            await _appManager.SetCallbackUrl(model.ApplicationId, model.Url, session,
                cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return Ok();
        }
    }

    public class AppCreateResult
    {
        public string SecretKey { get; set; }
        public string ApplicationId { get; set; }
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
        [Required]
        public string ApplicationId { get; set; }
        [Required]
        public string Url { get; set; }
    }
}