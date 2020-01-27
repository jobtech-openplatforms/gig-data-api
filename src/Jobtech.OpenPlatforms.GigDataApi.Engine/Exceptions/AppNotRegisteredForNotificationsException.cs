using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class AppNotRegisteredForNotificationsException : Exception
    {
        public AppNotRegisteredForNotificationsException(string message = null): base(message) { }
    }
}
