using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class AppDoesAlreadyExistException: Exception
    {
        public AppDoesAlreadyExistException(string message = null): base(message) { }
    }
}
