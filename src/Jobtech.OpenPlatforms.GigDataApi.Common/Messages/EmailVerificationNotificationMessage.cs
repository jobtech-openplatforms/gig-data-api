using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Common.Messages
{
    public class EmailVerificationNotificationMessage
    {
        public EmailVerificationNotificationMessage() { }

        public EmailVerificationNotificationMessage(string email, string userId,
            string appId)
        {
            Email = email;
            UserId = userId;
            AppId = appId;
        }

        public string Email { get; private set; }
        public string UserId { get; private set; }
        public string AppId { get; private set; }
    }
}
