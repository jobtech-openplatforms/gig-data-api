using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Raven.Client.Documents.Session;
using Rebus.Bus;
using PlatformData = Jobtech.OpenPlatforms.GigDataApi.Core.Entities.PlatformData;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IAppNotificationManager
    {
        Task NotifyEmailValidationDone(string userId, IList<string> appIds, string email, bool wasVerified,
            IAsyncDocumentSession session);

        Task NotifyPlatformConnectionAwaitingOAuthAuthentication(string userId, IList<string> appIds, string platformId, Guid externalPlatformId, string platformName,
            IAsyncDocumentSession session);

        Task NotifyPlatformConnectionAwaitingEmailVerification(string userId, IList<string> appIds, string platformId, Guid externalPlatformId, string platformName,
            IAsyncDocumentSession session);

        Task NotifyPlatformConnectionDataUpdate(string userId, IList<string> appIds,
            string platformId, Guid externalPlatformId, string platformName, string platformDataId, IAsyncDocumentSession session);

        Task NotifyPlatformConnectionRemoved(string userId, IList<string> appIds, string platformId, Guid externalPlatformId, string platformName,
            IAsyncDocumentSession session);
    }

    public class AppNotificationManager: IAppNotificationManager
    {
        private readonly IBus _bus;


        public AppNotificationManager(IBus bus)
        {
            _bus = bus;
        }

        public async Task NotifyEmailValidationDone(string userId, IList<string> appIds, string email, bool wasVerified,
            IAsyncDocumentSession session)
        {
            var user = await session.LoadAsync<User>(userId);
            var apps = await session.LoadAsync<App>(appIds);

            foreach (var app in apps.Values)
            {
                var emailVerificationNotificationEndpoint = app.EmailVerificationNotificationEndpoint;
                var message = new EmailVerificationNotificationMessage(emailVerificationNotificationEndpoint,app.SecretKey, email,
                    user.ExternalId, wasVerified);
                await _bus.Send(message);
            }
        }

        public async Task NotifyPlatformConnectionAwaitingOAuthAuthentication(string userId, IList<string> appIds, string platformId, Guid externalPlatformId, string platformName,
            IAsyncDocumentSession session)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, externalPlatformId, platformName, session, null, PlatformConnectionState.AwaitingOAuthAuthentication);
        }

        public async Task NotifyPlatformConnectionAwaitingEmailVerification(string userId, IList<string> appIds, string platformId, Guid externalPlatformId, string platformName,
            IAsyncDocumentSession session)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, externalPlatformId, platformName, session, null, PlatformConnectionState.AwaitingEmailVerification);
        }

        public async Task NotifyPlatformConnectionDataUpdate(string userId, IList<string> appIds,
            string platformId, Guid externalPlatformId, string platformName, string platformDataId, IAsyncDocumentSession session)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, externalPlatformId, platformName, session, platformDataId, PlatformConnectionState.Connected);
        }

        public async Task NotifyPlatformConnectionRemoved(string userId, IList<string> appIds, string platformId, Guid externalPlatformId, string platformName,
            IAsyncDocumentSession session)
        {
            await NotifyPlatformDataUpdate(userId, appIds, platformId, externalPlatformId, platformName, session, null, PlatformConnectionState.Removed);
        }

        private async Task NotifyPlatformDataUpdate(string userId, IList<string> appIds, string platformId, Guid externalPlatformId, string platformName, IAsyncDocumentSession session,
            string platformDataId = null, PlatformConnectionState connectionState = PlatformConnectionState.Connected)
        {
            var user = await session.LoadAsync<User>(userId);
            var apps = await session.LoadAsync<App>(appIds);

            PlatformData data = null;
            if (platformDataId != null)
            {
                data = await session.LoadAsync<PlatformData>(platformDataId);
            }

            if (connectionState == PlatformConnectionState.Connected && platformDataId != null)
            {
                connectionState = PlatformConnectionState.Synced;
            }


            foreach (var app in apps.Values)
            {
                Common.Messages.PlatformData messagePlatformData = null;
                if (data != null)
                {
                    var messageReviews = new List<Common.Messages.PlatformReview>();
                    if (data.Reviews != null)
                    {
                        foreach (var review in data.Reviews)
                        {
                            var messageReview = new Common.Messages.PlatformReview(review.ReviewIdentifier, review.ReviewText,
                                review.ReviewerName, review.ReviewHeading, review.ReviewerAvatarUri, review.RatingId, review.ReviewDate);
                            messageReviews.Add(messageReview);
                        }
                    }

                    var messageRatings = new List<Common.Messages.PlatformRating>();
                    if (data.Ratings != null)
                    {
                        foreach (var rating in data.Ratings)
                        {
                            var messageRating = new Common.Messages.PlatformRating(rating.Value, rating.Min, rating.Max, rating.SuccessLimit, rating.Identifier);
                            messageRatings.Add(messageRating);
                        }
                    }

                    var messageAchievements = new List<Common.Messages.PlatformAchievement>();
                    if (data.Achievements != null)
                    {
                        foreach (var achievement in data.Achievements)
                        {
                            PlatformAchievementScore score = null;
                            if (achievement.Score != null)
                            {
                                score = new PlatformAchievementScore(achievement.Score.Value, achievement.Score.Label);
                            }

                            var messageAchievement = new Common.Messages.PlatformAchievement(
                                achievement.AchievementIdentifier, achievement.Name,
                                achievement.AchievementPlatformType, achievement.AchievementType,
                                achievement.Description, achievement.ImageUri, score);

                            messageAchievements.Add(messageAchievement);
                        }
                    }


                    Common.Messages.PlatformRating messageAverageRating = null;
                    if (data.AverageRating != null)
                    {
                        messageAverageRating = new Common.Messages.PlatformRating(data.AverageRating.Value,
                            data.AverageRating.Min, data.AverageRating.Max, data.AverageRating.SuccessLimit, data.AverageRating.Identifier);
                    }

                    messagePlatformData = new Common.Messages.PlatformData(data.NumberOfGigs, messageAverageRating,
                        data.PeriodStart, data.PeriodEnd, messageRatings, messageReviews, messageAchievements);

                }


                var message = new PlatformConnectionUpdateNotificationMessage(app.NotificationEndpoint, app.SecretKey,
                    platformId, externalPlatformId, platformName, connectionState, user.ExternalId, DateTimeOffset.UtcNow, messagePlatformData);

                await _bus.Send(message);
            }
        }
    }
}
