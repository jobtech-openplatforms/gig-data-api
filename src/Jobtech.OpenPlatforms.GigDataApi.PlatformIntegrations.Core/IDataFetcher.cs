using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core
{
    public interface IDataFetcher<TConnectionInfo>
        where TConnectionInfo: IPlatformConnectionInfo
    {
        Task<TConnectionInfo> StartDataFetch(string userId, string platformId, TConnectionInfo connectionInfo,
            PlatformConnection platformConnection, CancellationToken cancellationToken = default);
    }
}
