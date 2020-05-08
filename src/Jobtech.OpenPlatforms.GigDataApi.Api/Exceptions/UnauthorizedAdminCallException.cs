using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Exceptions
{
    public class UnauthorizedAdminCallException : Exception
    {
        public UnauthorizedAdminCallException(string message = null): base(message) { }
    }
}
