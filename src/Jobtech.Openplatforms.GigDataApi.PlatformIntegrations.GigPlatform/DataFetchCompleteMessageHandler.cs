using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Common.Extensions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Exceptions;
using Jobtech.OpenPlatforms.GigDataCommon.Library.Messages;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Retry.Simple;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform
{
    public class DataFetchCompleteMessageHandler: IHandleMessages<PlatformUserUpdateDataMessage>, IHandleMessages<IFailed<PlatformUserUpdateDataMessage>>
    {
        private readonly IGigPlatformDataFetcher _gigPlatformDataFetcher;
        private readonly IntermittentDataManager _intermittentDataManager;
        private readonly IBus _bus;
        private readonly ILogger<DataFetchCompleteMessageHandler> _logger;
        

        public DataFetchCompleteMessageHandler(IGigPlatformDataFetcher gigPlatformDataFetcher, IntermittentDataManager intermittentDataManager, IBus bus, 
            ILogger<DataFetchCompleteMessageHandler> logger)
        {
            _gigPlatformDataFetcher = gigPlatformDataFetcher;
            _intermittentDataManager = intermittentDataManager;
            _bus = bus;
            _logger = logger;
        }

        public async Task Handle(PlatformUserUpdateDataMessage message)
        {
            _logger.BeginPropertyScope((LoggerPropertyNames.GigPlatformApiRequestId, message.RequestId), ("ResultType", message.ResultType));

            _logger.LogInformation($"Got data fetch result for request id {message.RequestId}");

            string userId = null;
            string platformId = null;
            string syncLogId = null;

            try
            {
                (userId, platformId, syncLogId) = await _intermittentDataManager.GetRequestData(message.RequestId);
            }
            catch (CacheDataNotFoundException)
            {
                _logger.LogInformation($"Could not find data in cache for requestId: {message.RequestId}.");
                var headers = MessageContext.Current.Headers;
                var retryCount = 0;
                if (!headers.ContainsKey("op-retry-count") || (int.TryParse(headers["op-retry-count"], out retryCount) && retryCount < 5))
                {
                    retryCount++;
                    var additionalHeader = new Dictionary<string, string>() {{ "op-retry-count", retryCount.ToString()}};
                    await _bus.DeferLocal(TimeSpan.FromSeconds(30), message, additionalHeader);
                    return;
                }

                _logger.LogError($"Could not find request data for requestId: {message.RequestId}. Will ignore message.");
                throw;
            }
            
            if (message.PlatformData != null)
            {
                var numberOfGigs = message.PlatformData.Interactions.Count;
                var periodStart = message.PlatformData.Interactions.Min(i => i.Period?.Start);
                var periodEnd = message.PlatformData.Interactions.Max(i => i.Period?.End);

                var ratings = new List<KeyValuePair<string, GigDataCommon.Library.Models.Rating>>();
                foreach (var interaction in message.PlatformData.Interactions)
                {
                    foreach (var rating in interaction.Outcome.Ratings)
                    {
                        ratings.Add(new KeyValuePair<string, GigDataCommon.Library.Models.Rating>(interaction.Id, rating));
                    }
                }

                var mappedRatings = new List<RatingDataFetchResult>();
                var interactionIdsToReviewIdentifiers = new Dictionary<string, IList<Guid>>();
                foreach (var rating in ratings)
                {

                    var mappedRating = new RatingDataFetchResult(rating.Value.Value);
                    mappedRatings.Add(mappedRating);

                    var ratingKey = rating.Key;
                    if (string.IsNullOrEmpty(ratingKey))
                    {
                        ratingKey = Guid.NewGuid().ToString();
                    }

                    if (!interactionIdsToReviewIdentifiers.ContainsKey(ratingKey))
                    {
                        interactionIdsToReviewIdentifiers.Add(ratingKey, new List<Guid>());
                    }
                    interactionIdsToReviewIdentifiers[ratingKey].Add(mappedRating.Identifier);
                }

                RatingDataFetchResult averageRating = null;
                if (mappedRatings.Count > 0)
                {
                    var averageRatingValue = mappedRatings.Average(r => r.Value);
                    averageRating = new RatingDataFetchResult(averageRatingValue);
                }

                var mappedReviews = new List<ReviewDataFetchResult>();
                foreach (var interaction in message.PlatformData.Interactions)
                {
                    var interactionId = interaction.Id;
                    if (string.IsNullOrEmpty(interactionId))
                    {
                        interactionId = Guid.NewGuid().ToString();
                    }


                    if (interaction.Outcome.Review == null)
                    {
                        continue;
                    }

                    Guid? ratingIdentifier = null;
                    if (interactionIdsToReviewIdentifiers.ContainsKey(interactionId) && interactionIdsToReviewIdentifiers[interactionId].Count > 0)
                    {
                        ratingIdentifier = interactionIdsToReviewIdentifiers[interactionId][0];
                    }

                    var clientName = interaction.Client?.Name;
                    var clientAvatarUri = interaction.Client?.PhotoUri;

                    var mappedReview = new ReviewDataFetchResult($"{platformId}_{interactionId}",
                        interaction.Period.End, ratingIdentifier, interaction.Outcome.Review.Title,
                        interaction.Outcome.Review.Text, clientName, clientAvatarUri);

                    mappedReviews.Add(mappedReview);
                }

                var mappedAchievements = new List<AchievementFetchResult>();
                foreach (var achievement in message.PlatformData.Achievements)
                {
                    var achievementId = achievement.Id;
                    if (string.IsNullOrEmpty(achievementId))
                    {
                        achievementId = Guid.NewGuid().ToString();
                    }

                    AchievementScoreFetchResult achievementScore = null;
                    if (achievement.Score != null)
                    {
                        achievementScore = new AchievementScoreFetchResult(achievement.Score.Value.ToString(CultureInfo.InvariantCulture), achievement.Score.Label);
                    }

                    Common.PlatformAchievementType achievementType = PlatformAchievementType.Badge;
                    switch (achievement.Type)
                    {
                        case GigDataCommon.Library.Models.PlatformAchievementType.Badge:
                            achievementType = PlatformAchievementType.Badge;
                            break;
                        case GigDataCommon.Library.Models.PlatformAchievementType.QualificationAssessment:
                            achievementType = PlatformAchievementType.QualificationAssessment;
                            break;
                        default:
                            throw new ArgumentException($"Unknown platform achievement type {achievement.Type}");
                    }

                    var mappedAchievement = new AchievementFetchResult(achievementId, achievement.Name,
                        achievement.Type.ToString(), achievementType, achievement.Description,
                        achievement.BadgeIconUri, achievementScore);

                    mappedAchievements.Add(mappedAchievement);
                }


                var platformDataFetchResult = new PlatformDataFetchResult(
                    numberOfGigs, periodStart, periodEnd, averageRating, mappedRatings, mappedReviews,
                    mappedAchievements, message.PlatformData.RawData);


                await _gigPlatformDataFetcher.CompleteDataFetching(userId, platformId, platformDataFetchResult, syncLogId);
            }
            else
            {
                if (message.ResultType == PlatformDataUpdateResultType.Succeess || 
                    message.ResultType == PlatformDataUpdateResultType.MalformedDataResponse)
                {
                    _logger.LogInformation("Got result type {ResultType}. Will complete data fetching.", message.ResultType);
                    await _gigPlatformDataFetcher.CompleteDataFetching(userId, platformId, null, syncLogId);
                } 
                else if (message.ResultType == PlatformDataUpdateResultType.UserNotFound)
                {
                    _logger.LogInformation("Got result type {ResultType}. User was not found. Will complete data fetching and signal that connection should be removed.",
                        message.ResultType);
                    //we should remove the connection
                    await _gigPlatformDataFetcher.CompleteDataFetchingWithConnectionRemoved(userId, platformId, 
                        PlatformConnectionDeleteReason.UserDidNotExist, syncLogId);
                } 
                else
                {
                    _logger.LogInformation("Got result type {ResultType}. Will schdule new data fetch.", message.ResultType);
                    //communication error, we should enqueue a new request to try to retrieve the data again.
                    var fetchMessage = new FetchDataForPlatformConnectionMessage(userId, platformId, PlatformIntegrationType.GigDataPlatformIntegration);
                    await _bus.DeferLocal(new TimeSpan(0,0,30), fetchMessage);
                }
            }
        }

        public async Task Handle(IFailed<PlatformUserUpdateDataMessage> message)
        {
            if (message.Exceptions.Any(e => e.GetType() == typeof(CacheDataNotFoundException)))
            {
                throw new ApplicationException();
            }

            _logger.LogInformation($"Handling failed {nameof(PlatformUserUpdateDataMessage)}. Will defer 60 seconds");
            await _bus.DeferLocal(TimeSpan.FromSeconds(60), message.Message);

        }
    }
}