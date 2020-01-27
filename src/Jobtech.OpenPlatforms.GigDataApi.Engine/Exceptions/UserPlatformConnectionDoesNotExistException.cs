namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class UserPlatformConnectionDoesNotExistException : Exception
    {
        public UserPlatformConnectionDoesNotExistException(Guid externalPlatformId, string message = null) :
            base(message)
        {
            ExternalPlatformId = externalPlatformId;
        }

        public Guid ExternalPlatformId { get; private set; }
    }
}