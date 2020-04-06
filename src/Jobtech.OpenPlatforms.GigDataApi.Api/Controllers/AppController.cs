using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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
        public async Task<AppViewModel> GetAppInfo(string applicationId, CancellationToken cancellationToken)
        {
            using var session = _documentStore.OpenAsyncSession();
            var app = await _appManager.GetAppFromApplicationId(applicationId, session, cancellationToken);
            return new AppViewModel(app.Name, app.ApplicationId);
        }

        [HttpGet("available")]
        [AllowAnonymous]
        public async Task<IList<AppViewModel>> GetAppInfos(CancellationToken cancellationToken, [FromQuery] int page = 0, [FromQuery] int pageSize = 20)
        {
            using var session = _documentStore.OpenAsyncSession();
            var apps = await _appManager.GetAllActiveApps(page, pageSize, session, cancellationToken);
            return apps.Select(a => new AppViewModel(a.Name, a.ApplicationId)).ToList();
        }


    }

    public class AppViewModel
    {
        public AppViewModel(string name, string applicationId)
        {
            Name = name;
            ApplicationId = applicationId;
        }

        public string ApplicationId { get; private set; }
        public string Name { get; private set; }
    }
}