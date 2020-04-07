using Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class App : BaseEntity
    {
        private App() : base()
        {
        }

        public App(string name, string secretKey, string applicationId, string notificationEndpoint,
            string emailVerificationNotificationEndpoint, bool isInactive) : this()
        {
            Name = name;
            SecretKey = secretKey;
            ApplicationId = applicationId;
            NotificationEndpoint = notificationEndpoint;
            EmailVerificationNotificationEndpoint = emailVerificationNotificationEndpoint;
            IsInactive = isInactive;
        }

        public string Name { get; set; }
        public string NotificationEndpoint { get; set; }
        public string EmailVerificationNotificationEndpoint { get; set; }
        public string SecretKey { get; set; }
        public string ApplicationId { get; private set; }
        public bool IsInactive { get; set; }
    }
}
