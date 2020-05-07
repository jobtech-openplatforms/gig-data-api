using System;
using System.Net;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class ExternalServiceErrorException: Exception
    {
        public ExternalServiceErrorException(HttpStatusCode? statusCode = null, string message = null, Exception innerException = null): base(message, innerException) 
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode? StatusCode { get; }
    }
}
