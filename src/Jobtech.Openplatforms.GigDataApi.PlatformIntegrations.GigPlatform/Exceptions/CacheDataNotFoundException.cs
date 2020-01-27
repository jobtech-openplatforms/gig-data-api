using System;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Exceptions
{
    public class CacheDataNotFoundException : Exception
    {
        public CacheDataNotFoundException(string message = null) : base(message) { }
    }
}
