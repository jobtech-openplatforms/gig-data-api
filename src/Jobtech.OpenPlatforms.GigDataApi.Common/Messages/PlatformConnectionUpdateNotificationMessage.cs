﻿using System;
using System.Collections.Generic;

namespace Jobtech.OpenPlatforms.GigDataApi.Common.Messages
{
    public class PlatformConnectionUpdateNotificationMessage: AppNotificationMessageBase
    {
        private PlatformConnectionUpdateNotificationMessage(): base() { }

        public PlatformConnectionUpdateNotificationMessage(string notificationEndpoint, string sharedSecret, string platformId, Guid externalPlatformId, 
            string platformName, PlatformConnectionState platformConnectionState, Guid userId, DateTimeOffset updated,
            PlatformData platformData = null, NotificationReason reason = NotificationReason.DataUpdate): base(notificationEndpoint, sharedSecret)
        {
            PlatformId = platformId;
            ExternalPlatformId = externalPlatformId;
            PlatformName = platformName;
            PlatformConnectionState = platformConnectionState;
            UserId = userId;
            Updated = updated;
            PlatformData = platformData;
        }

        public string PlatformId { get; private set; }
        public Guid ExternalPlatformId { get; private set; }
        public string PlatformName { get; private set; }
        public PlatformConnectionState PlatformConnectionState { get; private set; }
        public Guid UserId { get; private set; }
        public DateTimeOffset Updated { get; private set; }
        public PlatformData PlatformData { get; private set; }
        public NotificationReason Reason { get; private set; }
    }

    public enum NotificationReason
    {
        DataUpdate,
        ConnectionDeleted
    }

    public class PlatformData
    {
        public PlatformData(int numberOfGigs, PlatformRating averageRating, DateTimeOffset? periodStart,
            DateTimeOffset? periodEnd, IList<PlatformRating> ratings, IList<PlatformReview> reviews,
            IList<PlatformAchievement> achievements)
        {
            NumberOfGigs = numberOfGigs;
            AverageRating = averageRating;
            PeriodStart = periodStart;
            PeriodEnd = periodEnd;
            Ratings = ratings;
            Reviews = reviews;
            Achievements = achievements;
        }

        public int NumberOfGigs { get; private set; }
        public DateTimeOffset? PeriodStart { get; private set; }
        public DateTimeOffset? PeriodEnd { get; private set; }
        public PlatformRating AverageRating { get; private set; }
        public IList<PlatformReview> Reviews { get; private set; }
        public IList<PlatformRating> Ratings { get; private set; }
        public IList<PlatformAchievement> Achievements { get; private set; }
    }

    public class PlatformReview
    {
        public PlatformReview(string reviewId, string reviewText, string reviewerName, string reviewHeading, string reviewerAvatarUri, Guid? ratingId,
            DateTimeOffset? reviewDate)
        {
            ReviewId = reviewId;
            ReviewText = reviewText;
            ReviewerName = reviewerName;
            ReviewHeading = reviewHeading;
            ReviewerAvatarUri = reviewerAvatarUri;
            RatingId = ratingId;
            ReviewDate = reviewDate;
        }

        public string ReviewId { get; private set; }
        public DateTimeOffset? ReviewDate { get; private set; }
        public Guid? RatingId { get; private set; }
        public string ReviewHeading { get; private set; }
        public string ReviewText { get; private set; }
        public string ReviewerName { get; private set; }
        public string ReviewerAvatarUri { get; private set; }
    }

    public class PlatformRating
    {
        public PlatformRating(decimal value, decimal min, decimal max, decimal successLimit, Guid identifier)
        {
            Value = value;
            Min = min;
            Max = max;
            SuccessLimit = successLimit;
            Identifier = identifier;
        }

        public Guid Identifier { get; private set; }
        public decimal Min { get; private set; }
        public decimal Max { get; private set; }
        public decimal Value { get; private set; }
        public decimal SuccessLimit { get; private set; }
    }

    public class PlatformAchievement
    {
        public PlatformAchievement(string achievementId, string name, string achievementPlatformType,
            PlatformAchievementType achievementType, string description, string imageUri,
            PlatformAchievementScore score)
        {
            AchievementId = achievementId;
            Name = name;
            AchievementPlatformType = achievementPlatformType;
            AchievementType = achievementType;
            Description = description;
            ImageUri = imageUri;
            Score = score;
        }

        public string AchievementId { get; private set; }
        public string Name { get; private set; }
        public string AchievementPlatformType { get; private set; }
        public PlatformAchievementType AchievementType { get; private set; }
        public string Description { get; private set; }
        public string ImageUri { get; private set; }
        public PlatformAchievementScore Score { get; private set; }
    }

    public class PlatformAchievementScore
    {
        public PlatformAchievementScore(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; private set; }
        public string Label { get; private set; }
    }
}
