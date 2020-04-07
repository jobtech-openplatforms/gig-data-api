using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IAppManager
    {
        Task<(App, Auth0App)> CreateApp(string name, string notificationEndpoint,
            string emailVerificationNotificationEndpoint, string authCallbackUri,
            string description, string logoUrl, IAsyncDocumentSession session, bool isInactive = false,
            CancellationToken cancellationToken = default);

        Task<App> CreateApp(string name, string applicationId, string secretKey,
            string notificationEndpoint,
            string emailVerificationNotificationEndpoint, string description, string logoUrl,
            IAsyncDocumentSession session, bool isInactive = false,
            CancellationToken cancellationToken = default);

        Task<(App, Auth0App)> GetAppInfoFromApplicationId(string applicationId, IAsyncDocumentSession session,
            CancellationToken cancellationToken);

        Task<App> GetAppFromApplicationId(string applicationId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task<IList<App>> GetAppsFromApplicationIds(IList<string> applicationIds,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task<App> GetAppFromSecretKey(string secretKey, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task<App> GetAppFromId(string id, IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task<IEnumerable<App>> GetAppsFromIds(IList<string> ids, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<App>> GetAllActiveApps(int page, int pageSize, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task SetNotificationEndpointUrl(string applicationId, string url, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task SetEmailVerificationNotificationEndpointUrl(string applicationId, string url,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task SetCallbackUrl(string applicationId, string url, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task SetName(string applicationId, string name, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task SetDescription(string applicationId, string description, IAsyncDocumentSession session,
            CancellationToken cancellationToken);

        Task SetLogoUrl(string applicationId, string logoUrl, IAsyncDocumentSession session,
            CancellationToken cancellationToken);

        Task RotateSecret(string applicationId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);
    }

    public class AppManager : IAppManager
    {
        private readonly Auth0ManagementApiHttpClient _httpClient;

        public AppManager(Auth0ManagementApiHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<(App, Auth0App)> CreateApp(string name, string notificationEndpoint,
            string emailVerificationNotificationEndpoint, string authCallbackUri, 
            string description, string logoUrl, IAsyncDocumentSession session, bool isInactive = false,
            CancellationToken cancellationToken = default)
        {
            var auth0App = await _httpClient.CreateApp(name, authCallbackUri, cancellationToken);
            var app = await CreateApp(name, auth0App.ClientId, Guid.NewGuid().ToString(), notificationEndpoint,
                emailVerificationNotificationEndpoint, description, logoUrl, session, isInactive, cancellationToken);

            return (app, auth0App);
        }

        public async Task<App> CreateApp(string name, string applicationId, string secretKey,
            string notificationEndpoint,
            string emailVerificationNotificationEndpoint, string description, string logoUrl,
            IAsyncDocumentSession session, bool isInactive = false,
            CancellationToken cancellationToken = default)
        {
            var existingAppWithApplicationId =
                await session.Query<App>()
                    .SingleOrDefaultAsync(a => a.ApplicationId == applicationId, cancellationToken);

            if (existingAppWithApplicationId != null)
            {
                throw new AppDoesAlreadyExistException($"App with application id '{applicationId}' does already exist");
            }

            notificationEndpoint = string.IsNullOrWhiteSpace(notificationEndpoint) ? null : notificationEndpoint;
            emailVerificationNotificationEndpoint = string.IsNullOrWhiteSpace(emailVerificationNotificationEndpoint)
                ? null
                : emailVerificationNotificationEndpoint;

            var app = new App(name, secretKey, applicationId, notificationEndpoint,
                emailVerificationNotificationEndpoint, description, logoUrl, isInactive);
            await session.StoreAsync(app, cancellationToken);
            return app;
        }

        public async Task<(App, Auth0App)> GetAppInfoFromApplicationId(string applicationId,
            IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var app = await GetAppFromApplicationId(applicationId, session, cancellationToken);
            var auth0App = await _httpClient.GetApp(applicationId, cancellationToken);

            return (app, auth0App);
        }

        public async Task<App> GetAppFromApplicationId(string applicationId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await session.Query<App>()
                .SingleOrDefaultAsync(a => a.ApplicationId == applicationId, cancellationToken);

            if (app == null)
            {
                throw new AppDoesNotExistException($"App with application id {applicationId} does not exist");
            }

            return app;
        }

        public async Task<IList<App>> GetAppsFromApplicationIds(IList<string> applicationIds,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var apps = await session.Query<App>().Where(a => a.ApplicationId.In(applicationIds))
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

        public async Task SetNotificationEndpointUrl(string applicationId, string url, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(applicationId, session, cancellationToken);
            app.NotificationEndpoint = string.IsNullOrWhiteSpace(url) ? null : url;
        }

        public async Task SetEmailVerificationNotificationEndpointUrl(string applicationId, string url,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(applicationId, session, cancellationToken);
            app.EmailVerificationNotificationEndpoint = string.IsNullOrWhiteSpace(url) ? null : url;
        }

        public async Task SetCallbackUrl(string applicationId, string url, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(applicationId, session, cancellationToken);
            var _ = await _httpClient.UpdateCallbackUris(app.ApplicationId, url, cancellationToken);
        }

        public async Task SetName(string applicationId, string name, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(applicationId, session, cancellationToken);
            app.Name = name;
        }

        public async Task SetDescription(string applicationId, string description, IAsyncDocumentSession session,
            CancellationToken cancellationToken)
        {
            var app = await GetAppFromApplicationId(applicationId, session, cancellationToken);
            app.Description = description;
        }

        public async Task SetLogoUrl(string applicationId, string logoUrl, IAsyncDocumentSession session,
            CancellationToken cancellationToken)
        {
            var app = await GetAppFromApplicationId(applicationId, session, cancellationToken);
            app.LogoUrl = logoUrl;
        }

        public async Task RotateSecret(string applicationId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var app = await GetAppFromApplicationId(applicationId, session, cancellationToken);
            app.SecretKey = Guid.NewGuid().ToString();
        }
    }
}
