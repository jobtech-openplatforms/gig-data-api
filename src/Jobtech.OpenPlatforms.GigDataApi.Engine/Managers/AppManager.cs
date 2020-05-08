using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IAppManager
    {
        Task<App> CreateApp(string name, string dataUpdateCallbackUrl,
            string authorizationCallbackUrl,
            string description, string logoUrl, string websiteUrl, PlatformDataClaim platformDataClaim,
            IAsyncDocumentSession session, bool isInactive = false,
            CancellationToken cancellationToken = default);

        Task<App> GetAppFromApplicationId(Guid externalId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task<IList<App>> GetAppsFromApplicationIds(IList<Guid> externalIds,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task<App> GetAppFromSecretKey(string secretKey, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task<App> GetAppFromId(string id, IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task<IEnumerable<App>> GetAppsFromIds(IList<string> ids, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<App>> GetAllActiveApps(int page, int pageSize, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task SetDataUpdateCallbackUrl(Guid externalId, string url, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task SetAuthorizationCallbackUrl(Guid externalId, string url,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task SetName(Guid externalId, string name, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task SetDescription(Guid externalId, string description, IAsyncDocumentSession session,
            CancellationToken cancellationToken);

        Task SetLogoUrl(Guid externalId, string logoUrl, IAsyncDocumentSession session,
            CancellationToken cancellationToken);

        Task SetWebsiteUrl(Guid externalId, string websiteUrl, IAsyncDocumentSession session,
            CancellationToken cancellationToken);

        Task SetDefaultPlatformDataClaim(Guid externalId, PlatformDataClaim platformDataClaim, IAsyncDocumentSession session,
            CancellationToken cancellationToken);

        Task<string> RotateSecret(Guid externalId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);
    }

    public class AppManager : IAppManager
    {
        public AppManager()
        {
        }

        public async Task<App> CreateApp(string name, string dataUpdateCallbackUrl,
            string authorizationCallbackUrl,
            string description, string logoUrl, string websiteUrl, PlatformDataClaim platformDataClaim, 
            IAsyncDocumentSession session, bool isInactive = false,
            CancellationToken cancellationToken = default)
        {
            dataUpdateCallbackUrl = string.IsNullOrWhiteSpace(dataUpdateCallbackUrl) ? null : dataUpdateCallbackUrl;
            authorizationCallbackUrl = string.IsNullOrWhiteSpace(authorizationCallbackUrl)
                ? null
                : authorizationCallbackUrl;

            var app = new App(name, Guid.NewGuid(), Guid.NewGuid().ToString(), dataUpdateCallbackUrl,
                authorizationCallbackUrl, description, logoUrl, websiteUrl, platformDataClaim, isInactive);
            await session.StoreAsync(app, cancellationToken);
            return app;
        }

        public async Task<App> GetAppFromApplicationId(Guid externalId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await session.Query<App>()
                .SingleOrDefaultAsync(a => a.ExternalId == externalId, cancellationToken);

            if (app == null)
            {
                throw new AppDoesNotExistException($"App with external id {externalId} does not exist");
            }

            return app;
        }

        public async Task<IList<App>> GetAppsFromApplicationIds(IList<Guid> externalIds,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var apps = await session.Query<App>().Where(a => a.ExternalId.In(externalIds))
                .ToListAsync(cancellationToken);
            return apps;
        }

        public async Task<App> GetAppFromSecretKey(string secretKey, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await session.Query<App>().SingleOrDefaultAsync(a => a.SecretKey == secretKey, cancellationToken);


            if (app == null)
            {
                throw new AppDoesNotExistException($"App with given secret does not exist");
            }

            return app;
        }

        public async Task<App> GetAppFromId(string id, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            return await session.LoadAsync<App>(id, cancellationToken);
        }

        public async Task<IEnumerable<App>> GetAppsFromIds(IList<string> ids, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var apps = await session.LoadAsync<App>(ids, cancellationToken);
            return apps.Values;
        }

        public async Task<IEnumerable<App>> GetAllActiveApps(int page, int pageSize, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var apps = await session.Query<App>()
                .Where(a => !a.IsInactive)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return apps;
        }

        public async Task SetDataUpdateCallbackUrl(Guid externalId, string url, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(externalId, session, cancellationToken);
            app.DataUpdateCallbackUrl = string.IsNullOrWhiteSpace(url) ? null : url;
        }

        public async Task SetAuthorizationCallbackUrl(Guid externalId, string url,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(externalId, session, cancellationToken);
            app.AuthorizationCallbackUrl = string.IsNullOrWhiteSpace(url) ? null : url;
        }

        public async Task SetName(Guid externalId, string name, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(externalId, session, cancellationToken);
            app.Name = name;
        }

        public async Task SetDescription(Guid externalId, string description, IAsyncDocumentSession session,
            CancellationToken cancellationToken)
        {
            var app = await GetAppFromApplicationId(externalId, session, cancellationToken);
            app.Description = description;
        }

        public async Task SetLogoUrl(Guid externalId, string logoUrl, IAsyncDocumentSession session,
            CancellationToken cancellationToken)
        {
            var app = await GetAppFromApplicationId(externalId, session, cancellationToken);
            app.LogoUrl = logoUrl;
        }

        public async Task SetWebsiteUrl(Guid externalId, string websiteUrl, IAsyncDocumentSession session,
            CancellationToken cancellationToken)
        {
            var app = await GetAppFromApplicationId(externalId, session, cancellationToken);
            app.WebsiteUrl = websiteUrl;
        }

        public async Task SetDefaultPlatformDataClaim(Guid externalId, PlatformDataClaim platformDataClaim, IAsyncDocumentSession session,
            CancellationToken cancellationToken)
        {
            var app = await GetAppFromApplicationId(externalId, session, cancellationToken);
            app.DefaultPlatformDataClaim = platformDataClaim;
        }

        public async Task<string> RotateSecret(Guid externalId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(externalId, session, cancellationToken);
            app.SecretKey = Guid.NewGuid().ToString();
            return app.SecretKey;
        }
    }
}
