using Jobtech.OpenPlatforms.GigDataApi.Common;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Messages
{
    public class PlatformConnectionRemovedMessage
    {
        public PlatformConnectionRemovedMessage(string userId, string platformId, PlatformConnectionDeleteReason? platformConnectionDeleteReason = null)
        {
            UserId = userId;
            PlatformId = platformId;
            DeleteReason = platformConnectionDeleteReason;
        }

        public string UserId { get; private set; }
        public string PlatformId { get; private set; }
        public PlatformConnectionDeleteReason? DeleteReason { get; private set; }
    }
}
