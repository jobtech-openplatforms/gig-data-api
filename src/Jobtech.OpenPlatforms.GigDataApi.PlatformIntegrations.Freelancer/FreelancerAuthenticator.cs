using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core.OAuth;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Clients;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Options = Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models.Options;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer
{
    public interface IFreelancerAuthenticator : IAuthenticator { }

    public class FreelancerAuthenticator: IFreelancerAuthenticator
    {
        private readonly string _authInitEndpointUri;

        private const string AuthCompleteEndpoint = "token";

        private readonly string _redirectUri;

        private readonly string _appId;
        private readonly string _clientSecret;

        private const string Scope = "basic";
        private const string AdvancedScopes = "2 6";
        private const string ResponseType = "code";

        private readonly FreelancerOAuthClient _httpClient;

        public FreelancerAuthenticator(FreelancerOAuthClient httpClient, IOptions<Options> options)
        {
            _httpClient = httpClient;

            _appId = options.Value.AppId;
            _clientSecret = options.Value.ClientSecret;
            _authInitEndpointUri = $"{options.Value.AuthEndpointUri}";
            _redirectUri = options.Value.AuthRedirectUri;
            
        }

        public string GetAuthorizationUrl(Guid userId, string redirectUrl, Guid applicationId, PlatformDataClaim? platformDataClaim)
        {

            var state = new OAuthState(userId, redirectUrl, applicationId, platformDataClaim);

            var stateString = JsonConvert.SerializeObject(state);

            var uriStr = $"{_authInitEndpointUri}?" +
                         $"response_type={HttpUtility.UrlEncode(ResponseType)}&" +
                         $"client_id={HttpUtility.UrlEncode(_appId)}&" +
                         $"redirect_uri={HttpUtility.UrlEncode(_redirectUri)}&" +
                         $"scope={HttpUtility.UrlEncode(Scope)}&" +
                         $"advanced_scopes={HttpUtility.UrlEncode(AdvancedScopes)}&" +
                         $"state={HttpUtility.UrlEncode(stateString)}";

            return uriStr;
        }

        public async Task<OAuthCompleteResult> CompleteAuthorization(string authorizationGrantCode, string stateStr)
        {
            var response = await _httpClient.Client.PostAsync(AuthCompleteEndpoint, new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", authorizationGrantCode),
                new KeyValuePair<string, string>("client_id", _appId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri)
            }));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Could not get token from grant code");
            }

            var jsonStr = await response.Content.ReadAsStringAsync();

            var token = JsonConvert.DeserializeObject<OAuthAccessToken>(jsonStr);

            var state = JsonConvert.DeserializeObject<OAuthState>(stateStr);

            return new OAuthCompleteResult(state.RedirectUrl, state.UserId, state.ApplicationId, token, state.PlatformDataClaim);
        }

        public async Task<OAuthAccessToken> RefreshToken(OAuthAccessToken token)
        {
            var response = await _httpClient.Client.PostAsync(AuthCompleteEndpoint, new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", token.RefreshToken),
                new KeyValuePair<string, string>("client_id", _appId),
                new KeyValuePair<string, string>("client_secret", _clientSecret)
            }));

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException($"Was not allowed to refresh token");
                }

                throw new Exception("Could not refresh token");
            }

            var jsonStr = await response.Content.ReadAsStringAsync();

            var newToken = JsonConvert.DeserializeObject<OAuthAccessToken>(jsonStr);

            return newToken;
        }
    }

    public class OAuthState
    {
        public OAuthState(Guid userId, string redirectUrl, Guid applicationId, PlatformDataClaim? platformDataClaim)
        {
            UserId = userId;
            RedirectUrl = redirectUrl;
            ApplicationId = applicationId;
            PlatformDataClaim = platformDataClaim;
        }

        public Guid UserId { get; set; }
        public string RedirectUrl { get; set; }
        public Guid ApplicationId { get; set; }
        public PlatformDataClaim? PlatformDataClaim { get; set; }
    }
}
