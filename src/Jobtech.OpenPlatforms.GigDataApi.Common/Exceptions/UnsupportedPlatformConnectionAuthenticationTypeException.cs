using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Common.Exceptions
{
    public class UnsupportedPlatformConnectionAuthenticationTypeException: Exception
    {
        public UnsupportedPlatformConnectionAuthenticationTypeException(string message = null): base(message) { }
    }
}
