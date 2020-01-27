namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Messages
{
    public class PlatformConnectionRemovedMessage
    {
        public PlatformConnectionRemovedMessage(string userId, string platformId)
        {
            UserId = userId;
            PlatformId = platformId;
        }

        public string UserId { get; private set; }
        public string PlatformId { get; private set; }
    }
}
