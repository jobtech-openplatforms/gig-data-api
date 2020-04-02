using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class ExternalResourceCommunicationErrorException : Exception
    {
        public ExternalResourceCommunicationErrorException(string message = null, Exception innerException = null) :
            base(message, innerException)
        {
        }
    }
}
