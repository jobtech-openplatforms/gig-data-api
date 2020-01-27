namespace Jobtech.OpenPlatforms.GigDataApi.Common.Messages
{
    public abstract class AppNotificationMessageBase
    {
        protected AppNotificationMessageBase()
        {

        }

        protected AppNotificationMessageBase(string notificationEndpoint, string sharedSecret)
        {
            NotificationEndpoint = notificationEndpoint;
            SharedSecret = sharedSecret;
        }

        public string NotificationEndpoint { get; private set; }
        public string SharedSecret { get; private set; }
    }
}