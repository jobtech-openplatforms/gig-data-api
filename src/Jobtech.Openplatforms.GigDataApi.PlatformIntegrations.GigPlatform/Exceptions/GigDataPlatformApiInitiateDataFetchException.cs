using System;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Exceptions
{
    public class GigDataPlatformApiInitiateDataFetchException: Exception
    {
        public GigDataPlatformApiInitiateDataFetchException(string message = null): base(message) { }
    }
}
