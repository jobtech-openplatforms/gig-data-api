using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class PlatformAlreadyExistsException : Exception
    {
        public PlatformAlreadyExistsException(string message = null) : base(message) { }
    }

    public class PlatformDoesNotSupportAutomaticConnection : Exception
    {
        public PlatformDoesNotSupportAutomaticConnection(string message = null): base(message) { }
    }
}
