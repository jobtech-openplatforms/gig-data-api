using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Core.OAuth;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Clients;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer.Models;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer
{
    public interface IFreelancerDataFetcher : IDataFetcher<OAuthPlatformConnectionInfo> { }

    public class FreelancerDataFetcher: DataFetcherBase<OAuthPlatformConnectionInfo>, IFreelancerDataFetcher
    {
        private readonly FreelancerApiClient _apiClient;
        private readonly IFreelancerAuthenticator _freelancerAuthenticator;
        private readonly ILogger<FreelancerDataFetcher> _logger;

        public FreelancerDataFetcher(FreelancerApiClient apiClient, IFreelancerAuthenticator authenticator, IBus bus, ILogger<FreelancerDataFetcher> logger): base(bus)
        {
            _apiClient = apiClient;
            _freelancerAuthenticator = authenticator;
            _logger = logger;
        }

        public new async Task<OAuthPlatformConnectionInfo> StartDataFetch(string userId, string platformId, OAuthPlatformConnectionInfo connectionInfo, PlatformConnection platformConnection)
        {
            _logger.LogTrace($"Will start data fetch from Freelancer for user with id {userId}");
            if (connectionInfo.Token.HasExpired())
            {
                _logger.LogInformation($"Token has expired, will try to refresh it");
                try
                {
                    var newToken = await _freelancerAuthenticator.RefreshToken(new OAuthAccessToken
                    {
                        AccessToken = connectionInfo.Token.AccessToken,
                        RefreshToken = connectionInfo.Token.RefreshToken,
                        ExpiresIn = connectionInfo.Token.ExpiresInSeconds
                    });

                    connectionInfo.Token = new Token(newToken);
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogInformation("The consent to access Freelancer seems tot have been revoked. Will act accordingly");
                    return await HandleUnauthorized(userId, platformId);
                }

            }

            _apiClient.SetAccessToken(connectionInfo.Token.AccessToken);

            UserInfoApiResult userProfile = null;
            string rawUserProfile = null;
            ReviewApiResult freelancerReviews = null;
            string rawReviews = null;

            try
            {
                (userProfile, rawUserProfile) = await _apiClient.GetUserProfile();
                (freelancerReviews, rawReviews) = await _apiClient.GetReviews(userProfile.Result.Id);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogInformation("The consent to access Freelancer seems tot have been revoked. Will act accordingly");
                return await HandleUnauthorized(userId, platformId);
            }

            var (ratings, reviews) = GetRatingsAndReviews(freelancerReviews);

            var raw = $"{{data: [{rawUserProfile}, {rawReviews}]}}";

            var numberOfCompletedJobs = userProfile.Result.Reputation.EntireHistory.Complete;
            var averageRating = numberOfCompletedJobs == 0 ? null : new RatingDataFetchResult(userProfile.Result.Reputation.EntireHistory.Overall);

            //achievements
            var achievements = new List<AchievementFetchResult>();

            var qualifications = userProfile.Result.Qualifications;
            if (qualifications != null)
            {
                foreach (var qualification in qualifications)
                {
                    var score = new AchievementScoreFetchResult(
                        qualification.ScorePercentage.ToString(CultureInfo.InvariantCulture), "percent");

                    var achievement = new AchievementFetchResult($"freelancer_qualification_{qualification.Id}", qualification.Name,
                        "qualification", PlatformAchievementType.QualificationAssessment, qualification.Description,
                        $"https://www.freelancer.com{qualification.IconUrl}",
                        score);

                    achievements.Add(achievement);
                }
            }

            var badges = userProfile.Result.Badges;
            if (badges != null)
            {
                foreach (var badge in badges)
                {
                    var achievement = new AchievementFetchResult($"freelancer_badge_{badge.Id}", badge.Name, "badge",
                        PlatformAchievementType.Badge, badge.Description, $"https://www.freelancer.com{badge.IconUrl}", null);

                    achievements.Add(achievement);
                }
            }

            var registrationDate = DateTimeOffset.FromUnixTimeSeconds(userProfile.Result.RegistrationDate);

            var result = new PlatformDataFetchResult(numberOfCompletedJobs,
                registrationDate, DateTimeOffset.UtcNow, averageRating, ratings, reviews, achievements, raw);
            await CompleteDataFetch(userId, platformId ,result);


            _logger.LogTrace($"Freelancer data fetch completed for user with id {userId}");
            return connectionInfo;
        }

        private async Task<OAuthPlatformConnectionInfo> HandleUnauthorized(string userId, string platformId)
        {
            //we do no longer have access to freelancer for the given connection
            await CompleteDataFetchWithConnectionRemoved(userId, platformId);
            return new OAuthPlatformConnectionInfo(null) { IsDeleted = true };
        }

        private (IList<RatingDataFetchResult> Ratings, IList<ReviewDataFetchResult> Reviews) GetRatingsAndReviews(
            ReviewApiResult reviewApiResult)
        {
            var ratings = new List<RatingDataFetchResult>();
            var reviews = new List<ReviewDataFetchResult>();
            foreach (var freelancerReview in reviewApiResult.Result.Reviews)
            {
                var reviewerInfo =
                    reviewApiResult.Result.Users.SingleOrDefault(u => u.Id == freelancerReview.FromUserId);
                var reviewerAvatarUrl = reviewerInfo != null ? $"https:{reviewerInfo.AvatarCdn}" : null;


                var rating = new RatingDataFetchResult(freelancerReview.Rating);
                var review = new ReviewDataFetchResult($"freelancer_{freelancerReview.ReviewContext.ContextId}",
                    DateTimeOffset.FromUnixTimeSeconds(freelancerReview.TimeSubmitted), rating.Identifier, null,
                    freelancerReview.Description, reviewerInfo?.DisplayName, reviewerAvatarUrl);

                ratings.Add(rating);
                reviews.Add(review);
            }

            return (ratings, reviews);
        }
    }
}
