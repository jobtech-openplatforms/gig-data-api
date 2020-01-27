﻿namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
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
        public async Task<ActionResult<AppCreateResult>> CreateApp([FromHeader(Name = "admin-key")] Guid adminKey, [FromBody] AppCreateModel model)
        {
            if (!_options.AdminKeys.Contains(adminKey))
            {
                return Unauthorized();
            }

            using (var session = _documentStore.OpenAsyncSession())
            {
                var createdApp = await _appManager.CreateApp(model.Name, model.NotificationEndpointUrl,
                    model.EmailVerificationNotificationEndpointUrl, model.AuthCallbackUrl, session);

                await session.SaveChangesAsync();

                return new AppCreateResult
                {
                    SecretKey = createdApp.SecretKey,
                    ApplicationId = createdApp.ApplicationId
                };
            }
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
        [Required]
        public string NotificationEndpointUrl { get; set; }
        [Required]
        public string EmailVerificationNotificationEndpointUrl { get; set; }
        [Required]
        public string AuthCallbackUrl { get; set; }
    }
}