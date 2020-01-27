using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions
{
    public class UserPlatformConnectionAlreadyExistsException : Exception
    {
        public UserPlatformConnectionAlreadyExistsException(string platformId, string message = null) :
            base(message)
        {
            PlatformId = platformId;
        }

        public string PlatformId { get; private set; }
    }
}
