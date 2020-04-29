using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AppController : ControllerBase
    {
        private readonly IAppManager _appManager;
        private readonly IDocumentStore _documentStore;

        public AppController(IAppManager appManager, IDocumentStore documentStore)
        {
            _appManager = appManager;
            _documentStore = documentStore;
        }

        [HttpGet("{applicationId}")]
        [AllowAnonymous]
        [Produces("application/json")]
        public async Task<AppViewModel> GetAppInfo(Guid applicationId, CancellationToken cancellationToken)
        {
            using var session = _documentStore.OpenAsyncSession();
            var app = await _appManager.GetAppFromApplicationId(applicationId, session, cancellationToken);
            return new AppViewModel(app.ExternalId.ToString(), app.Name, app.Description, app.LogoUrl, app.WebsiteUrl,
                app.AuthorizationCallbackUrl, app.DefaultPlatformDataClaim);
        }

        [HttpGet("available")]
        [AllowAnonymous]
        [Produces("application/json")]
        public async Task<IList<AppViewModel>> GetAppInfos(CancellationToken cancellationToken,
            [FromQuery] int page = 0, [FromQuery] int pageSize = 20)
        {
            using var session = _documentStore.OpenAsyncSession();
            var apps = await _appManager.GetAllActiveApps(page, pageSize, session, cancellationToken);
            return apps.Select(a => new AppViewModel(a.ExternalId.ToString(), a.Name, a.Description, a.LogoUrl,
                    a.WebsiteUrl, a.AuthorizationCallbackUrl, a.DefaultPlatformDataClaim))
                .ToList();
        }


    }

    public class AppViewModel
    {
        public AppViewModel(string applicationId, string name, string description, string logoUrl, string websiteUrl,
            string authorizationCallbackUrl, PlatformDataClaim defaultPlatformDataClaim)
        {
            ApplicationId = applicationId;
            Name = name;
            Description = description;
            LogoUrl = logoUrl;
            WebsiteUrl = websiteUrl;
            AuthorizationCallbackUrl = authorizationCallbackUrl;
            DefaultPlatformDataClaim = defaultPlatformDataClaim;
        }

        public string ApplicationId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string LogoUrl { get; private set; }
        public string WebsiteUrl { get; private set; }
        public string AuthorizationCallbackUrl { get; private set; }
        public PlatformDataClaim DefaultPlatformDataClaim { get; private set; }
    }
}