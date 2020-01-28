using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Messages
{
    public class FetchDataForPlatformConnectionMessage
    {
        private FetchDataForPlatformConnectionMessage()
        {

        }

        public FetchDataForPlatformConnectionMessage(string userId, string platformId, PlatformIntegrationType platformIntegrationType,
            IPlatformConnectionInfo connectionInfo)
        {
            UserId = userId;
            PlatformId = platformId;
            PlatformIntegrationType = platformIntegrationType;
            ConnectionInfo = connectionInfo;
        }

        public string UserId { get; private set; }
        public string PlatformId { get; private set; }
        public PlatformIntegrationType PlatformIntegrationType { get; private set; }
        public IPlatformConnectionInfo ConnectionInfo { get; private set; }
    }
}