using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Messages
{
    public class DataFetchCompleteMessage
    {
        private DataFetchCompleteMessage() { }

        public DataFetchCompleteMessage(string userId, string platformId, PlatformDataFetchResult result)
        {
            UserId = userId;
            PlatformId = platformId;
            Result = result;
        }

        public PlatformDataFetchResult Result { get; private set; }
        public string UserId { get; private set; }
        public string PlatformId { get; private set; }
    }
}