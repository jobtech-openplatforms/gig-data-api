namespace Jobtech.OpenPlatforms.GigDataApi.Common.Messages
{
    public class FetchDataForPlatformConnectionMessage
    {
        private FetchDataForPlatformConnectionMessage()
        {

        }

        public FetchDataForPlatformConnectionMessage(string userId, string platformId,
            PlatformIntegrationType platformIntegrationType)
        {
            UserId = userId;
            PlatformId = platformId;
            PlatformIntegrationType = platformIntegrationType;
        }

        public string UserId { get; private set; }
        public string PlatformId { get; private set; }
        public PlatformIntegrationType PlatformIntegrationType { get; private set; }
    }
}