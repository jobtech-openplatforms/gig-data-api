using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Jobtech.OpenPlatforms.GigDataApi.Notifications.Functions
{
    public static class PlatformConnectionUpdatedNotifier
    {
        [FunctionName("PlatformConnectionUpdateHandler")]
        [return: ServiceBus("platformconnection.update", Connection = "ServiceBusConnectionString")]
        public static async Task<Message> Run([ServiceBusTrigger("platformconnection.update", Connection = "ServiceBusConnectionString")]Message message, string lockToken, MessageReceiver messageReceiver, ILogger log)
        {
            var body = System.Text.Encoding.UTF8.GetString(message.Body);

            var platformConnectionUpdateNotificationMessage =
                JsonConvert.DeserializeObject<PlatformConnectionUpdateNotificationMessage>(body);

            if (string.IsNullOrWhiteSpace(platformConnectionUpdateNotificationMessage.NotificationEndpoint))
            {
                log.LogWarning(
                    $"No notification endpoint was given for notifying app with shared secret {platformConnectionUpdateNotificationMessage.SharedSecret} where to be notified for a platform connection update for user {platformConnectionUpdateNotificationMessage.UserId} and platform with id {platformConnectionUpdateNotificationMessage.PlatformId}");
                return null;
            }

            var payload = new PlatformConnectionUpdateNotificationPayload
            {
                PlatformId = platformConnectionUpdateNotificationMessage.ExternalPlatformId,
                UserId = platformConnectionUpdateNotificationMessage.UserId,
                AppSecret = platformConnectionUpdateNotificationMessage.SharedSecret,
                PlatformName = platformConnectionUpdateNotificationMessage.PlatformName,
                PlatformConnectionState = platformConnectionUpdateNotificationMessage.PlatformConnectionState,
                Updated = platformConnectionUpdateNotificationMessage.Updated.ToUnixTimeSeconds(),
                Reason = platformConnectionUpdateNotificationMessage.Reason
            };

            if (platformConnectionUpdateNotificationMessage.PlatformData != null)
            {
                var platformDataPayload = new PlatformDataPayload
                {
                    NumberOfGigs = platformConnectionUpdateNotificationMessage.PlatformData.NumberOfGigs,
                    PeriodStart = platformConnectionUpdateNotificationMessage.PlatformData.PeriodStart,
                    PeriodEnd = platformConnectionUpdateNotificationMessage.PlatformData.PeriodEnd,
                    NumberOfRatings = platformConnectionUpdateNotificationMessage.PlatformData.Ratings?.Count ?? 0
                };

                if (platformConnectionUpdateNotificationMessage.PlatformData.AverageRating != null)
                {
                    platformDataPayload.AverageRating =
                        new PlatformRatingPayload(
                            platformConnectionUpdateNotificationMessage.PlatformData.AverageRating);
                }

                if (platformConnectionUpdateNotificationMessage.PlatformData.Ratings != null && 
                    platformConnectionUpdateNotificationMessage.PlatformData.Ratings.Count > 0)
                {
                    platformDataPayload.NumberOfRatingsThatAreDeemedSuccessful =
                        platformConnectionUpdateNotificationMessage.PlatformData.Ratings.Count(r => r.Value >= r.SuccessLimit);
                }
                

                if (platformConnectionUpdateNotificationMessage.PlatformData.Reviews != null)
                {
                    var reviewPayloads = new List<PlatformReviewPayload>();
                    foreach (var platformDataReview in platformConnectionUpdateNotificationMessage.PlatformData?.Reviews)
                    {
                        var platformReviewPayload = new PlatformReviewPayload
                        {
                            ReviewId = platformDataReview.ReviewId,
                            ReviewDate = platformDataReview.ReviewDate,
                            ReviewerName = platformDataReview.ReviewerName,
                            ReviewText = platformDataReview.ReviewText,
                            ReviewHeading = platformDataReview.ReviewHeading,
                            ReviewerAvatarUri = platformDataReview.ReviewerAvatarUri
                        };

                        if (platformDataReview.RatingId.HasValue)
                        {
                            platformReviewPayload.Rating = new PlatformRatingPayload(
                                platformConnectionUpdateNotificationMessage.PlatformData.Ratings?.Single(r =>
                                    r.Identifier == platformDataReview.RatingId));
                        }

                        reviewPayloads.Add(platformReviewPayload);
                    }

                    if (reviewPayloads.Any())
                    {
                        platformDataPayload.Reviews = reviewPayloads;
                    }
                }

                if (platformConnectionUpdateNotificationMessage.PlatformData.Achievements != null)
                {
                    var achievementPayloads = new List<PlatformAchievementPayload>();

                    foreach (var platformDataAchievement in platformConnectionUpdateNotificationMessage.PlatformData
                        ?.Achievements)
                    {
                        PlatformAchievementScorePayload scorePayload = null;
                        if (platformDataAchievement.Score != null)
                        {
                            scorePayload = new PlatformAchievementScorePayload
                            {
                                Value = platformDataAchievement.Score.Value,
                                Label = platformDataAchievement.Score.Label
                            };
                        }

                        var platformAchievementPayload = new PlatformAchievementPayload
                        {
                            AchievementId = platformDataAchievement.AchievementId,
                            AchievementPlatformType = platformDataAchievement.AchievementPlatformType,
                            AchievementType = platformDataAchievement.AchievementType,
                            Description = platformDataAchievement.Description,
                            ImageUrl = platformDataAchievement.ImageUri,
                            Name = platformDataAchievement.Name,
                            Score = scorePayload
                        };

                        achievementPayloads.Add(platformAchievementPayload);
                    }

                    platformDataPayload.Achievements = achievementPayloads;
                }

                payload.PlatformData = platformDataPayload;
            }
            else
            {
                payload.PlatformData = null;
            }

            var httpClient = new HttpClient { BaseAddress = new Uri(platformConnectionUpdateNotificationMessage.NotificationEndpoint) };

            try
            {
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                var serializedPayload = JsonConvert.SerializeObject(payload, serializerSettings);
                log.LogInformation($"Payload to be sent to {platformConnectionUpdateNotificationMessage.NotificationEndpoint}: {serializedPayload}");

                var content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await httpClient.PostAsync(
                    platformConnectionUpdateNotificationMessage.NotificationEndpoint,
                    content);
                if (!response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    log.LogError($"Got non 200 status code ({response.StatusCode}). Got response: '{responseText}'. Will defer message and try again later.");
                    var newMessage = MessageUtility.GetDeferredMessage(message, body, 10, log);
                    if (newMessage == null)
                    {
                        await messageReceiver.DeadLetterAsync(lockToken, "Message retried too many times");
                        return null;
                    }
                    
                    //await messageReceiver.CompleteAsync(lockToken);
                    return newMessage;
                }
            }
            catch (Exception e)
            {
                log.LogError(e, $"Got exception when calling endpoint {platformConnectionUpdateNotificationMessage.NotificationEndpoint}");
                var newMessage = MessageUtility.GetDeferredMessage(message, body, 10, log);
                await messageReceiver.CompleteAsync(lockToken);
                return newMessage;
            }

            log.LogInformation($"Call to endpoint {platformConnectionUpdateNotificationMessage.NotificationEndpoint} succeeded");

            return null;
        }
    }

    public class PlatformConnectionUpdateNotificationPayload
    {
        public Guid PlatformId { get; set; }
        public string PlatformName { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public PlatformConnectionState PlatformConnectionState { get; set; }
        public Guid UserId { get; set; }
        public long Updated { get; set; }
        public PlatformDataPayload PlatformData { get; set; }
        public string AppSecret { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public NotificationReason Reason { get; set; }
    }

    public class PlatformDataPayload
    {
        public int NumberOfGigs { get; set; }
        public int NumberOfRatings { get; set; }
        public int NumberOfRatingsThatAreDeemedSuccessful { get; set; }
        [JsonConverter(typeof(YearMonthDayDateTimeConverter))]
        public DateTimeOffset? PeriodStart { get; set; }
        [JsonConverter(typeof(YearMonthDayDateTimeConverter))]
        public DateTimeOffset? PeriodEnd { get; set; }
        public PlatformRatingPayload AverageRating { get; set; }
        public IList<PlatformReviewPayload> Reviews { get; set; }
        public IList<PlatformAchievementPayload> Achievements { get; set; }
    }

    public class PlatformReviewPayload
    {
        public string ReviewId { get; set; }
        [JsonConverter(typeof(YearMonthDayDateTimeConverter))]
        public DateTimeOffset? ReviewDate { get; set; }
        public PlatformRatingPayload Rating { get; set; }
        public string ReviewHeading { get; set; }
        public string ReviewText { get; set; }
        public string ReviewerName { get; set; }
        public string ReviewerAvatarUri { get; set; }
    }

    public class PlatformAchievementPayload
    {
        public string AchievementId { get; set; }
        public string Name { get; set; }
        public string AchievementPlatformType { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public PlatformAchievementType AchievementType { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public PlatformAchievementScorePayload Score { get; set; }
    }

    public class PlatformAchievementScorePayload
    {
        public string Value { get; set; }
        public string Label { get; set; }
    }

    public class PlatformRatingPayload
    {
        public PlatformRatingPayload(PlatformRating rating)
        {
            Value = rating.Value;
            Min = rating.Min;
            Max = rating.Max;
            IsSuccessful = rating.Value >= rating.SuccessLimit;
        }

        public decimal Value { get; set; }
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public bool IsSuccessful { get; set; }
    }

    internal class YearMonthDayDateTimeConverter : IsoDateTimeConverter
    {
        public YearMonthDayDateTimeConverter()
        {
            DateTimeFormat = "yyyy-MM-dd";
        }
    }
}
