using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jobtech.OpenPlatforms.GigDataApi.Common.Extensions
{
    public static class LoggerExtensions
    {
        public static IDisposable BeginNamedScope(this ILogger logger, string name,
            params ValueTuple<string, object>[] properties)
        {
            var dictionary = properties.ToDictionary(p => p.Item1, p => p.Item2);
            dictionary[name + ".Scope"] = Guid.NewGuid();
            return logger.BeginScope(dictionary);
        }

        public static IDisposable BeginNamedScopeWithMessage(this ILogger logger, string name, string messageId,
            params ValueTuple<string, object>[] properties)
        {
            var dictionary = properties.ToDictionary(p => p.Item1, p => p.Item2);
            dictionary[name + ".Scope"] = Guid.NewGuid();
            dictionary["messageId"] = messageId;
            return logger.BeginScope(dictionary);
        }

        public static IDisposable BeginPropertyScope(this ILogger logger,
            params ValueTuple<string, object>[] properties)
        {
            var dictionary = properties.ToDictionary(p => p.Item1, p => p.Item2);
            return logger.BeginScope(dictionary);
        }
    }

    public static class LoggerPropertyNames
    {
        public const string PlatformId = "PlatformId";
        public const string PlatformName = "PlatformName";
        public const string PlatformDataId = "PlatformDataId";
        public const string PlatformIntegrationType = "PlatformIntegrationType";
        public const string UserId = "UserId";
        public const string GigPlatformApiRequestId = "GigPlatformApiRequestId";
        public const string NotificationEndpoint = "NotificationEndpoint";
        public const string NotificationEndpointSharedSecret = "NotificationEndpointSharedSecret";
        public const string ApplicationId = "ApplicationId";

    }
}
