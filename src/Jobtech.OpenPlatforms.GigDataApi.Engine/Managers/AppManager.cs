using System;
using System.Collections.Generic;
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
        Task<App> CreateApp(string name, string notificationEndpoint,
            string emailVerificationNotificationEndpoint, string authCallbackUri, IAsyncDocumentSession session);
        Task<App> CreateApp(string name, string applicationId, string secretKey, string notificationEndpoint,
            string emailVerificationNotificationEndpoint, IAsyncDocumentSession session);
        Task<App> GetAppFromApplicationId(string applicationId, IAsyncDocumentSession session);

        Task<IList<App>> GetAppsFromApplicationIds(IList<string> applicationIds,
            IAsyncDocumentSession session);
        Task<App> GetAppFromSecretKey(string secretKey, IAsyncDocumentSession session);
        Task<App> GetAppFromId(string id, IAsyncDocumentSession session);
        Task<IEnumerable<App>> GetAppsFromIds(IList<string> ids, IAsyncDocumentSession session);
    }

    public class AppManager: IAppManager
    {
        private readonly Auth0ManagementApiHttpClient _httpClient;

        public AppManager(Auth0ManagementApiHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<App> CreateApp(string name, string notificationEndpoint,
            string emailVerificationNotificationEndpoint, string authCallbackUri, IAsyncDocumentSession session)
        {
            var auth0App = await _httpClient.CreateApp(name, authCallbackUri);
            return await CreateApp(name, auth0App.ClientId, Guid.NewGuid().ToString(), notificationEndpoint,
                emailVerificationNotificationEndpoint, session);
        }

        public async Task<App> CreateApp(string name, string applicationId, string secretKey, string notificationEndpoint,
            string emailVerificationNotificationEndpoint, IAsyncDocumentSession session)
        {
            var existingAppWithApplicationId =
                await session.Query<App>().SingleOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (existingAppWithApplicationId != null)
            {
                throw new AppDoesAlreadyExistException($"App with application id {applicationId} does already exist");
            }

            var app = new App(name, secretKey, applicationId, notificationEndpoint,
                emailVerificationNotificationEndpoint);
            await session.StoreAsync(app);
            return app;
        }

        public async Task<App> GetAppFromApplicationId(string applicationId, IAsyncDocumentSession session)
        {
            var app = await session.Query<App>().SingleOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (app == null)
            {
                throw new AppDoesNotExistException($"App with application id {applicationId} does not exist");
            }

            return app;
        }

        public async Task<IList<App>> GetAppsFromApplicationIds(IList<string> applicationIds,
            IAsyncDocumentSession session)
        {
            var apps = await session.Query<App>().Where(a => a.ApplicationId.In(applicationIds)).ToListAsync();
            return apps;
        }

        public async Task<App> GetAppFromSecretKey(string secretKey, IAsyncDocumentSession session)
        {
            var app = await session.Query<App>().SingleOrDefaultAsync(a => a.SecretKey == secretKey);


            if (app == null)
            {
                throw new AppDoesNotExistException($"App with given secret does not exist");
            }

            return app;
        }

        public async Task<App> GetAppFromId(string id, IAsyncDocumentSession session)
        {
            return await session.LoadAsync<App>(id);
        }

        public async Task<IEnumerable<App>> GetAppsFromIds(IList<string> ids, IAsyncDocumentSession session)
        {
            var apps = await session.LoadAsync<App>(ids);
            return apps.Values;
        }
    }
}
