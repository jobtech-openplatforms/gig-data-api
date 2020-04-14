using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Common.Extensions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Raven.Client.Documents;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Retry.Simple;
using PlatformData = Jobtech.OpenPlatforms.GigDataApi.Core.Entities.PlatformData;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class PlatformConnectionUpdateHandler: IHandleMessages<PlatformConnectionUpdateNotificationMessage>, IHandleMessages<IFailed<PlatformConnectionUpdateNotificationMessage>>
    {
        private readonly IAppManager _appManager;
        private readonly IPlatformDataManager _platformDataManager;
        private readonly IDocumentStore _documentStore;
        private readonly RebusConfiguration _rebusConfiguration;
        private readonly IBus _bus;
        private readonly IMessageContext _messageContext;
        private readonly ILogger<PlatformConnectionUpdateHandler> _logger;

        private const int MaxMessageRetries = 100;

        public PlatformConnectionUpdateHandler(IAppManager appManager, IPlatformDataManager platformDataManager,
            IDocumentStore documentStore, IOptions<RebusConfiguration> rebusOptions,
            IBus bus, IMessageContext messageContext, ILogger<PlatformConnectionUpdateHandler> logger)
        {
            _appManager = appManager;
            _platformDataManager = platformDataManager;
            _documentStore = documentStore;
            _rebusConfiguration = rebusOptions.Value;
            _bus = bus;
            _messageContext = messageContext;
            _logger = logger;
        }

        public async Task Handle(PlatformConnectionUpdateNotificationMessage message)
        {
            using var loggerScope = _logger.BeginNamedScopeWithMessage(nameof(DataFetchCompleteHandler),
                _messageContext.Message.GetMessageId(),
                (LoggerPropertyNames.ApplicationId, message.AppId),
                (LoggerPropertyNames.UserId, message.UserId));

            using var session = _documentStore.OpenAsyncSession();

            var cancellationToken = _messageContext.GetCancellationToken();

            var user = await session.LoadAsync<User>(message.UserId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning(
                    $"User with given id does not exist. Will move message to error queue.");
                await _bus.Advanced.TransportMessage.Forward(_rebusConfiguration.ErrorQueueName);
                return;
            }

            var app = await _appManager.GetAppFromId(message.AppId, session, cancellationToken);

            if (app == null)
            {
                _logger.LogWarning(
                    $"App with given id does not exist. Will move message to error queue.");
                await _bus.Advanced.TransportMessage.Forward(_rebusConfiguration.ErrorQueueName);
                return;
            }

            var platform = await session.LoadAsync<Core.Entities.Platform>(message.PlatformId, cancellationToken);

            if (platform == null)
            {
                _logger.LogWarning(
                    $"Platform with given id does not exist. Will move message to error queue.");
                await _bus.Advanced.TransportMessage.Forward(_rebusConfiguration.ErrorQueueName);
                return;
            }

            var notificationReason = message.Reason;
            var platformConnectionState = message.PlatformConnectionState;

            if (notificationReason != NotificationReason.ConnectionDeleted)
            {
                if (platformConnectionState == PlatformConnectionState.Connected || platformConnectionState == PlatformConnectionState.Synced)
                {
                    var activePlatformConnection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == message.PlatformId && !pc.ConnectionInfo.IsDeleted);
                    if (activePlatformConnection == null)
                    {
                        _logger.LogWarning(
                            $"No active connection exists for the given platform. Will notify app about removed connection.");
                        notificationReason = NotificationReason.ConnectionDeleted;
                        platformConnectionState = PlatformConnectionState.Removed;
                    }
                    else
                    {
                        var notificationInfoForApp =
                            activePlatformConnection.ConnectionInfo.NotificationInfos.SingleOrDefault(ni =>
                                ni.AppId == app.Id);

                        if (notificationInfoForApp == null)
                        {
                            _logger.LogWarning(
                                $"No active connection to the given app. Will notify app about removed connection.");
                            notificationReason = NotificationReason.ConnectionDeleted;
                            platformConnectionState = PlatformConnectionState.Removed;
                        }
                    }
                }
            }

            PlatformData platformData = null;

            if (notificationReason == NotificationReason.DataUpdate)
            {
                platformData =
                    await _platformDataManager.GetPlatformData(user.Id, platform.Id, session, cancellationToken);
            }

            var updatePayload = CreatePayload(platform.ExternalId, user.ExternalId, app.SecretKey, platform.Name,
                platformConnectionState, DateTimeOffset.UtcNow, notificationReason, platformData);

            if (string.IsNullOrWhiteSpace(app.NotificationEndpoint))
            {
                _logger.LogWarning(
                    $"No notification endpoint was given. Will move message to error queue.");
                await _bus.Advanced.TransportMessage.Forward(_rebusConfiguration.ErrorQueueName);
                return;
            }

            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            
            var serializedPayload = JsonConvert.SerializeObject(updatePayload, serializerSettings);
            _logger.LogTrace("Payload to be sent: {Payload}", serializedPayload);

            var content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;

            try
            {
                var httpClient = new HttpClient();
                response = await httpClient.PostAsync(
                    new Uri(app.NotificationEndpoint),
                    content, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Got error calling endpoint. Will schedule retry.");
                await _bus.DeferMessageLocalWithExponentialBackOff(message, _messageContext.Headers, MaxMessageRetries,
                    _rebusConfiguration.ErrorQueueName, logger: _logger);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Got non success status code ({HttpStatusCode}) calling endpoint. Will schedule retry.", response.StatusCode);
                await _bus.DeferMessageLocalWithExponentialBackOff(message, _messageContext.Headers, MaxMessageRetries,
                    _rebusConfiguration.ErrorQueueName, logger: _logger);
                return;
            }

            _logger.LogInformation("App successfully notified about platform data update.");
        }

        private PlatformConnectionUpdateNotificationPayload CreatePayload(Guid externalPlatformId, Guid externalUserId,
            string appSecret, string platformName, PlatformConnectionState platformConnectionState,
            DateTimeOffset updated, NotificationReason notificationReason, PlatformData platformData = null)
        {
            var payload = new PlatformConnectionUpdateNotificationPayload
            {
                PlatformId = externalPlatformId,
                UserId = externalUserId,
                AppSecret = appSecret,
                PlatformName = platformName,
                PlatformConnectionState = platformConnectionState,
                Updated = updated.ToUnixTimeSeconds(),
                Reason = notificationReason
            };

            if (platformData != null)
            {
                var platformDataPayload = new PlatformDataPayload
                {
                    NumberOfGigs = platformData.NumberOfGigs,
                    PeriodStart = platformData.PeriodStart,
                    PeriodEnd = platformData.PeriodEnd,
                    NumberOfRatings = platformData.Ratings?.Count() ?? 0
                };

                if (platformData.AverageRating != null)
                {
                    platformDataPayload.AverageRating =
                        new PlatformRatingPayload(
                            platformData.AverageRating);
                }

                if (platformData.Ratings != null &&
                    platformData.Ratings.Any())
                {
                    platformDataPayload.NumberOfRatingsThatAreDeemedSuccessful =
                        platformData.Ratings.Count(r =>
                            r.Value >= r.SuccessLimit);
                }


                if (platformData.Reviews != null)
                {
                    var reviewPayloads = new List<PlatformReviewPayload>();
                    foreach (var platformDataReview in platformData.Reviews
                    )
                    {
                        var platformReviewPayload = new PlatformReviewPayload
                        {
                            ReviewId = platformDataReview.ReviewIdentifier,
                            ReviewDate = platformDataReview.ReviewDate,
                            ReviewerName = platformDataReview.ReviewerName,
                            ReviewText = platformDataReview.ReviewText,
                            ReviewHeading = platformDataReview.ReviewHeading,
                            ReviewerAvatarUri = platformDataReview.ReviewerAvatarUri
                        };

                        if (platformDataReview.RatingId.HasValue)
                        {
                            platformReviewPayload.Rating = new PlatformRatingPayload(
                                platformData.Ratings?.Single(r =>
                                    r.Identifier == platformDataReview.RatingId));
                        }

                        reviewPayloads.Add(platformReviewPayload);
                    }

                    if (reviewPayloads.Any())
                    {
                        platformDataPayload.Reviews = reviewPayloads;
                    }
                }

                if (platformData.Achievements != null)
                {
                    var achievementPayloads = new List<PlatformAchievementPayload>();

                    foreach (var platformDataAchievement in platformData.Achievements)
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
                            AchievementId = platformDataAchievement.AchievementIdentifier,
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

            return payload;
        }

        public Task Handle(IFailed<PlatformConnectionUpdateNotificationMessage> message)
        {
            throw new NotImplementedException();
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
        public PlatformRatingPayload(Rating rating)
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
