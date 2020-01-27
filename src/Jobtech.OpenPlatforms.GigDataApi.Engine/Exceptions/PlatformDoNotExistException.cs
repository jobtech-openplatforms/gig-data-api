using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class PlatformDoNotExistException : Exception
    {
        public PlatformDoNotExistException(string message = null) : base(message) { }
    }
}
