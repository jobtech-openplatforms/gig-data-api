using Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class App : BaseEntity
    {
        private App() : base()
        {
        }

        public App(string name, string secretKey, string applicationId, string notificationEndpoint, string emailVerificationNotificationEndpoint): this()
        {
            Name = name;
            SecretKey = secretKey;
            ApplicationId = applicationId;
            NotificationEndpoint = notificationEndpoint;
            EmailVerificationNotificationEndpoint = emailVerificationNotificationEndpoint;
        }

        public string Name { get; private set; }
        public string NotificationEndpoint { get; set; }
        public string EmailVerificationNotificationEndpoint { get; set; }
        public string SecretKey { get; private set; }
        public string ApplicationId { get; private set; }
    }
}
