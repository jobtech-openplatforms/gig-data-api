using System;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.OAuth;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core
{
    public interface IAuthenticator
    {
        string GetAuthorizationUrl(Guid userId, string redirectUrl, Guid applicationId);
        Task<OAuthCompleteResult> CompleteAuthorization(string authorizationGrantCode, string stateStr);
        Task<OAuthAccessToken> RefreshToken(OAuthAccessToken token);
    }
}
