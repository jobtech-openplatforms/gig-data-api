using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Raven.Client.Documents.Session;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IAppNotificationManager
    {
        Task NotifyPlatformConnectionAwaitingOAuthAuthentication(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task NotifyPlatformConnectionAwaitingEmailVerification(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task NotifyPlatformConnectionDataUpdate(string userId, IList<string> appIds,
            string platformId, IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task NotifyPlatformConnectionRemoved(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task NotifyPlatformConnectionSynced(string userId, IList<string> appIds, string platformId,
            string syncLogId, IAsyncDocumentSession session, CancellationToken cancellationToken = default);
    }

    public class AppNotificationManager : IAppNotificationManager
    {
        private readonly IBus _bus;
        private readonly IPlatformDataManager _platformDataManager;

        public AppNotificationManager(IBus bus, IPlatformDataManager platformDataManager)
        {
            _bus = bus;
            _platformDataManager = platformDataManager;
        }

        public async Task NotifyPlatformConnectionAwaitingOAuthAuthentication(string userId, IList<string> appIds,
            string platformId, IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, session, NotificationReason.DataUpdate,
                PlatformConnectionState.AwaitingOAuthAuthentication, cancellationToken: cancellationToken);
        }

        public async Task NotifyPlatformConnectionAwaitingEmailVerification(string userId, IList<string> appIds,
            string platformId, IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, session, NotificationReason.DataUpdate,
                PlatformConnectionState.AwaitingEmailVerification, cancellationToken: cancellationToken);
        }

        public async Task NotifyPlatformConnectionDataUpdate(string userId, IList<string> appIds,
            string platformId, IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var platformConnectionState = PlatformConnectionState.Connected;
            var data = await _platformDataManager.GetPlatformData(userId, platformId, session, cancellationToken);
            if (data != null)
            {
                platformConnectionState = PlatformConnectionState.Synced;
            }

            await NotifyPlatformDataUpdate(userId, appIds, platformId, session, NotificationReason.DataUpdate, platformConnectionState,
                cancellationToken: cancellationToken);
        }

        public async Task NotifyPlatformConnectionRemoved(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, session, NotificationReason.ConnectionDeleted, PlatformConnectionState.Removed,
                cancellationToken: cancellationToken);
        }

        public async Task NotifyPlatformConnectionSynced(string userId, IList<string> appIds, string platformId,
            string syncLogId, IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, session, NotificationReason.DataUpdate, PlatformConnectionState.Synced,
                syncLogId, cancellationToken);
        }

        private async Task NotifyPlatformDataUpdate(string userId, IList<string> appIds, string platformId,
            IAsyncDocumentSession session, NotificationReason reason, PlatformConnectionState connectionState = PlatformConnectionState.Connected,
            string syncLogId = null,
            CancellationToken cancellationToken = default)
        {
            var apps = await session.LoadAsync<App>(appIds, cancellationToken);

            foreach (var message in apps.Values.Select(app => new Common.Messages.PlatformConnectionUpdateNotificationMessage(platformId, userId,
                app.Id, connectionState, syncLogId, reason)))
            {
                await _bus.Send(message);
            }
        }
    }
}
