using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class AppDoesNotExistException : Exception
    {
        public AppDoesNotExistException(string message = null) : base(message) { }
    }
}