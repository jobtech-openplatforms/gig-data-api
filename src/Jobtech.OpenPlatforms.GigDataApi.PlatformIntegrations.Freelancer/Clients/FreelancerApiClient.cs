using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Clients
{
    public class FreelancerApiClient
    {

        private const string OauthHeaderName = "freelancer-oauth-v1";
        private string _accessToken;
        public FreelancerApiClient(HttpClient client)
        {
            Client = client;
        }

        public HttpClient Client { get; }


        public void SetAccessToken(string token)
        {
            _accessToken = token;
            if (Client.DefaultRequestHeaders.Contains(OauthHeaderName))
            {
                Client.DefaultRequestHeaders.Remove(OauthHeaderName);
            }

            Client.DefaultRequestHeaders.Add(OauthHeaderName, token);
        }

        public async Task<(UserInfoApiResult UserInfo, string RawResult)> GetUserProfile()
        {
            ValidateOAuthHeaderSet();

            var response = await Client.GetAsync(
                $"api/users/0.1/self/?jobs=true&reputation=true&badge_details=true&qualification_details=true");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Not authorized to access Freelancer");
                }

                throw new HttpRequestException("Unhandled error when calling Freelancer");
            }

            var requestResult = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<UserInfoApiResult>(requestResult);
            return (result, requestResult);

        }

        public async Task<(ReviewApiResult Reviews, string RawResult)> GetReviews(int userId)
        {
            ValidateOAuthHeaderSet();
            var requestResult = await Client.GetStringAsync($"api/projects/0.1/reviews/?to_users[]={userId}&user_details=true&user_profile_description=true&user_avatar=true&user_display_info=true&ratings=true");
            var result = JsonConvert.DeserializeObject<ReviewApiResult>(requestResult);

            var users = new List<ReviewResultUser>();

            dynamic json = JObject.Parse(requestResult);
            JObject usersObj = json["result"]["users"];
            if (usersObj != null)
            {
                var usersKeyValue =
                    JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(usersObj.ToString());

                foreach (var keyValuePair in usersKeyValue)
                {
                    ReviewResultUser user =
                        JsonConvert.DeserializeObject<ReviewResultUser>(keyValuePair.Value.ToString());
                    user.Id = Int32.Parse(keyValuePair.Key);
                    users.Add(user);
                }
            }

            result.Result.Users = users;

            return (result, requestResult);
        }

        private void ValidateOAuthHeaderSet()
        {
            if (!Client.DefaultRequestHeaders.Contains(OauthHeaderName))
            {
                throw new ArgumentException("Token must be set before making request");
            }
        }
    }
}