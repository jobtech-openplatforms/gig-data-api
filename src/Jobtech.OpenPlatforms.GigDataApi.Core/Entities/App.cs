using Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class App : BaseEntity
    {
        private App()
        {
        }

        public App(string name, string secretKey, string applicationId, string notificationEndpoint,
            string emailVerificationNotificationEndpoint, string description, string logoUrl, string websiteUrl,
            bool isInactive = false) : this()
        {
            Name = name;
            SecretKey = secretKey;
            ApplicationId = applicationId;
            NotificationEndpoint = notificationEndpoint;
            EmailVerificationNotificationEndpoint = emailVerificationNotificationEndpoint;
            Description = description;
            LogoUrl = logoUrl;
            WebsiteUrl = websiteUrl;
            IsInactive = isInactive;
        }

        public string Name { get; set; }
        public string NotificationEndpoint { get; set; }
        public string EmailVerificationNotificationEndpoint { get; set; }
        public string SecretKey { get; set; }
        public string ApplicationId { get; private set; }
        public bool IsInactive { get; set; }
        public string Description { get; set; }
        public string LogoUrl { get; set; }
        public string WebsiteUrl { get; set; }
    }
}
