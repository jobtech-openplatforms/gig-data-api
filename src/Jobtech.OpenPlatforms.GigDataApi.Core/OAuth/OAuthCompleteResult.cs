using System;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.OAuth
{
    public class OAuthCompleteResult
    {
        public OAuthCompleteResult(string redirectUrl, Guid userId, string applicationId, OAuthAccessToken token)
        {
            RedirectUrl = redirectUrl;
            UserId = userId;
            ApplicationId = applicationId;
            Token = token;
        }


        public OAuthAccessToken Token { get; }
        public Guid UserId { get; }
        public string RedirectUrl { get; }
        public string ApplicationId { get; }
    }
}
