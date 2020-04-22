using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class InvalidEmailAddressException: Exception
    {
        public InvalidEmailAddressException(string emailAddress, string message = null) : base(message)
        {
            EmailAddress = emailAddress;
        }

        public string EmailAddress { get; }
    }
}
