using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Engine.IoC;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IUserManager
    {
        Task<User> GetOrCreateUserIfNotExists(string uniqueIdentifier, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task<User> GetUserByUniqueIdentifier(string uniqueIdentifier, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task<User> GetUserByExternalId(Guid externalId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);
    }

    public class UserManager : IUserManager
    {
        private readonly Auth0ManagementApiHttpClient _auth0ManagementClient;

        public UserManager(Auth0ManagementApiHttpClient auth0ManagementClient)
        {
            _auth0ManagementClient = auth0ManagementClient;
        }

        public async Task<User> GetOrCreateUserIfNotExists(string uniqueIdentifier, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var userProfile = await _auth0ManagementClient.GetUserProfile(uniqueIdentifier);

            var existingUser = await GetExistingUserByUniqueIdentifier(uniqueIdentifier, session, cancellationToken);
            if (existingUser != null)
            {
                return existingUser;
            }

            var user = new User(uniqueIdentifier, userProfile.FirstName, userProfile.LastName);
            await session.StoreAsync(user, cancellationToken);
            return user;
        }

        public async Task<User> GetUserByUniqueIdentifier(string uniqueIdentifier, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var existingUser = await GetExistingUserByUniqueIdentifier(uniqueIdentifier, session, cancellationToken);
            if (existingUser == null)
            {
                throw new UserDoNotExistException($"Could not find user with unique identifier {uniqueIdentifier}");
            }

            return existingUser;
        }

        public async Task<User> GetUserByExternalId(Guid externalId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var existingUser = await session.Query<User>()
                .SingleOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken);
            if (existingUser == null)
            {
                throw new UserDoNotExistException($"Could not find user with external id {externalId}");
            }

            return existingUser;
        }

        private async Task<User> GetExistingUserByUniqueIdentifier(string uniqueIdentifier,
            IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            return await session.Query<User>()
                .SingleOrDefaultAsync(u => u.UniqueIdentifier == uniqueIdentifier, cancellationToken);
        }
    }

    public class Auth0ManagementApiHttpClient
    {
        private static AccessToken _accessToken;

        private readonly string _clientSecret;
        private readonly string _clientId;
        private readonly string _managementApiAudience;

        public Auth0ManagementApiHttpClient(HttpClient client,
            IOptions<CVDataEngineServiceCollectionExtension.Auth0Configuration> options)
        {
            Client = client;
            _clientId = options.Value.ManagementClientId;
            _clientSecret = options.Value.ManagementClientSecret;
            _managementApiAudience = options.Value.ManagementApiAudience;
        }

        public HttpClient Client { get; }

        public async Task<string> GetAccessToken(CancellationToken cancellationToken = default)
        {
            if (_accessToken == null || _accessToken.HasExpired())
            {
                var body = new AccessTokenBody(_clientId, _clientSecret, _managementApiAudience);
                var bodyJson = JsonConvert.SerializeObject(body);
                var response = await Client.PostAsync("/oauth/token",
                    new StringContent(bodyJson, Encoding.UTF8, "application/json"), cancellationToken);
                var responseStr = await response.Content.ReadAsStringAsync();
                var accessToken = JsonConvert.DeserializeObject<AccessToken>(responseStr);
                _accessToken = accessToken;
            }

            return _accessToken.Token;
        }

        public async Task<Auth0UserProfile> GetUserProfile(string userId, CancellationToken cancellationToken = default)
        {
            var accessToken = await GetAccessToken(cancellationToken);
            Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            var userProfileStr = await Client.GetStringAsync($"/api/v2/users/{userId}");
            return JsonConvert.DeserializeObject<Auth0UserProfile>(userProfileStr);
        }

        public async Task<Auth0App> CreateApp(string name, string callbackUri,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                name,
                callbacks = new List<string> {callbackUri},
                app_type = "regular_web"
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);

            var accessToken = await GetAccessToken(cancellationToken);
            Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            var response = await Client.PostAsync("/api/v2/clients",
                new StringContent(jsonPayload, Encoding.UTF8, "application/json"), cancellationToken);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Auth0App>(jsonResponse);
        }

        public async Task<Auth0App> UpdateCallbackUris(string clientId, string callbackUri, CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                callbacks = new List<string> {callbackUri}
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);

            var accessToken = await GetAccessToken(cancellationToken);
            Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            var response = await Client.PatchAsync($"/api/v2/clients/{clientId}",
                new StringContent(jsonPayload, Encoding.UTF8, "application/json"), cancellationToken);

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Auth0App>(jsonResponse);
        }

        internal class AccessToken
        {
            public AccessToken()
            {
                _created = DateTimeOffset.UtcNow;
            }

            [JsonProperty("access_token")] public string Token { get; private set; }

            [JsonProperty("expires_in")] public int ExpiresIn { get; private set; }
            private readonly DateTimeOffset _created;

            public bool HasExpired()
            {
                var now = DateTimeOffset.UtcNow;

                return now.Subtract(_created).TotalSeconds > ExpiresIn;
            }
        }

        internal class AccessTokenBody
        {
            public AccessTokenBody(string clientId, string clientSecret, string audience)
            {
                GrantType = "client_credentials";
                ClientId = clientId;
                ClientSecret = clientSecret;
                Audience = audience;
            }

            [JsonProperty("grant_type")] public string GrantType { get; }
            [JsonProperty("client_id")] public string ClientId { get; }
            [JsonProperty("client_secret")] public string ClientSecret { get; }
            [JsonProperty("audience")] public string Audience { get; }
        }
    }



    public class Auth0UserProfile
    {
        public string Email { get; set; }
        [JsonProperty("given_name")] public string FirstName { get; set; }
        [JsonProperty("family_name")] public string LastName { get; set; }
    }

    public class Auth0App
    {
        public string Name { get; set; }
        [JsonProperty("client_id")] public string ClientId { get; set; }
    }

    public class ApproveApiHttpClient
    {
        private readonly string _apiKey;
        private readonly string _confirmEmailCallback;
        private readonly string _rejectEmailCallback;

        public ApproveApiHttpClient(HttpClient client,
            IOptions<CVDataEngineServiceCollectionExtension.ApproveApiConfiguration> approveApiOptions)
        {
            Client = client;
            _apiKey = approveApiOptions.Value.ApiKey;
            _confirmEmailCallback = approveApiOptions.Value.ConfirmEmailCallbackUri;
            _rejectEmailCallback = approveApiOptions.Value.RejectEmailCallbackUri;
        }

        public HttpClient Client { get; }

        public async Task<(string PromptId, int ExpiresIn)> SendEmailValidationPrompt(string mailAddress,
            CancellationToken cancellationToken = default)
        {

            var bodyContent = $"Klicka på 'JA' knappen nedan för att validera att du äger mail-adressen {mailAddress}";

            var payload = new SendPromptPayload(mailAddress,
                bodyContent, "Ja", "Nej",
                _confirmEmailCallback, _rejectEmailCallback);

            SetAuthHeader();

            var payloadStr = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            var response = await Client.PostAsync("prompt",
                new StringContent(payloadStr, Encoding.UTF8, "application/json"), cancellationToken);

            var promptStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = JsonConvert.DeserializeObject<PromptErrorResponse>(promptStr);
                throw new ExternalServiceErrorException(
                    $"Got non-success response from ApproveApi. Response code: {response.StatusCode}, Error: {errorResponse.Error}");
            }

            var prompt = JsonConvert.DeserializeObject<Prompt>(promptStr);

            return (prompt.Id, prompt.SentAt + 86400);
        }

        public async Task<bool?> GetPromptAnswer(string promptId)
        {
            SetAuthHeader();
            var promptStr = await Client.GetStringAsync($"prompt/{promptId}");
            var prompt = JsonConvert.DeserializeObject<Prompt>(promptStr);

            return prompt.Answer?.Result;
        }

        private void SetAuthHeader()
        {
            var byteArray = Encoding.ASCII.GetBytes($"{_apiKey}:");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        internal class SendPromptPayload
        {
            public SendPromptPayload(string user, string body, string approveText = null, string rejectText = null,
                string approveRedirectUrl = null, string rejectRedirectUrl = null, int? expiresIn = null)
            {
                User = user;
                Body = body;
                ApproveText = approveText;
                RejectText = rejectText;
                ApproveRedirectUrl = approveRedirectUrl;
                RejectRedirectUrl = rejectRedirectUrl;
                ExpiresIn = expiresIn;
            }

            public string User { get; set; }
            public string Body { get; set; }
            [JsonProperty("approve_text")] public string ApproveText { get; set; }
            [JsonProperty("reject_text")] public string RejectText { get; set; }
            [JsonProperty("approve_redirect_url")] public string ApproveRedirectUrl { get; set; }
            [JsonProperty("reject_redirect_url")] public string RejectRedirectUrl { get; set; }
            [JsonProperty("expires_in")] public int? ExpiresIn { get; set; }
        }

        internal class Prompt
        {
            public string Id { get; set; }
            [JsonProperty("sent_at")] public int SentAt { get; set; }
            [JsonProperty("is_expired")] public bool IsExpired { get; set; }
            public PromptAnswer Answer { get; set; }
        }

        internal class PromptAnswer
        {
            public bool Result { get; set; }
            public int Time { get; set; }
        }

        internal class PromptErrorResponse
        {
            public string Error { get; set; }
        }
    }
}
