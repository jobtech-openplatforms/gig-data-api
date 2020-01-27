namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class ExternalServiceErrorException: Exception
    {
        public ExternalServiceErrorException(string message = null): base(message) { }
    }
}
