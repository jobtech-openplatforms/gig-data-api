using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Raven.Client.Documents.Session;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IAppNotificationManager
    {
        Task NotifyEmailValidationDone(string userId, IList<string> appIds, string email,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task NotifyPlatformConnectionAwaitingOAuthAuthentication(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task NotifyPlatformConnectionAwaitingEmailVerification(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task NotifyPlatformConnectionDataUpdate(string userId, IList<string> appIds,
            string platformId, IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task NotifyPlatformConnectionRemoved(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);
    }

    public class AppNotificationManager : IAppNotificationManager
    {
        private readonly IBus _bus;


        public AppNotificationManager(IBus bus)
        {
            _bus = bus;
        }

        public async Task NotifyEmailValidationDone(string userId, IList<string> appIds, string email,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var user = await session.LoadAsync<User>(userId, cancellationToken);
            var apps = await session.LoadAsync<App>(appIds, cancellationToken);

            foreach (var message in apps.Values.Select(app =>
                new Common.Messages.EmailVerificationNotificationMessage(email, user.Id, app.Id)))
            {
                await _bus.Send(message);
            }
        }

        public async Task NotifyPlatformConnectionAwaitingOAuthAuthentication(string userId, IList<string> appIds,
            string platformId, IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, session,
                PlatformConnectionState.AwaitingOAuthAuthentication, cancellationToken);
        }

        public async Task NotifyPlatformConnectionAwaitingEmailVerification(string userId, IList<string> appIds,
            string platformId, IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, session,
                PlatformConnectionState.AwaitingEmailVerification, cancellationToken);
        }

        public async Task NotifyPlatformConnectionDataUpdate(string userId, IList<string> appIds,
            string platformId, IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, session, PlatformConnectionState.Connected,
                cancellationToken);
        }

        public async Task NotifyPlatformConnectionRemoved(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, session, PlatformConnectionState.Removed,
                cancellationToken);
        }

        private async Task NotifyPlatformDataUpdate(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, PlatformConnectionState connectionState = PlatformConnectionState.Connected,
            CancellationToken cancellationToken = default)
        {
            var apps = await session.LoadAsync<App>(appIds, cancellationToken);

            foreach (var message in apps.Values.Select(app => new Common.Messages.PlatformConnectionUpdateNotificationMessage(platformId, userId,
                app.Id, connectionState)))
            {
                await _bus.Send(message);
            }
        }
    }
}
