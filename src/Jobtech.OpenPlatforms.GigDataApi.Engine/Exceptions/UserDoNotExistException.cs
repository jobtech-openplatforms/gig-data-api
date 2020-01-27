namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class UserDoNotExistException : Exception
    {
        public UserDoNotExistException(string message = null) : base(message) { }
    }
}
