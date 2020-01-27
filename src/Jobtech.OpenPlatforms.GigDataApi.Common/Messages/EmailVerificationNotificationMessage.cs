using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Common.Messages
{
    public class EmailVerificationNotificationMessage: AppNotificationMessageBase
    {
        private EmailVerificationNotificationMessage(): base()
        {

        }

        public EmailVerificationNotificationMessage(string notificationEndpoint, string sharedSecret, string email, Guid userId,
            bool wasVerified): base(notificationEndpoint, sharedSecret)
        {
            Email = email;
            UserId = userId;
            WasVerified = wasVerified;
        }

        public string Email { get; private set; }
        public Guid UserId { get; private set; }
        public bool WasVerified { get; private set; }
    }
}
