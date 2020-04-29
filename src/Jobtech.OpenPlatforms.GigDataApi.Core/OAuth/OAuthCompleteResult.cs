using System;
using Jobtech.OpenPlatforms.GigDataApi.Common;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.OAuth
{
    public class OAuthCompleteResult
    {
        public OAuthCompleteResult(string redirectUrl, Guid userId, Guid applicationId, OAuthAccessToken token, PlatformDataClaim? platformDataClaim)
        {
            RedirectUrl = redirectUrl;
            UserId = userId;
            ApplicationId = applicationId;
            Token = token;
            PlatformDataClaim = platformDataClaim;
        }


        public OAuthAccessToken Token { get; }
        public Guid UserId { get; }
        public string RedirectUrl { get; }
        public Guid ApplicationId { get; }
        public PlatformDataClaim? PlatformDataClaim { get; }
    }
}
