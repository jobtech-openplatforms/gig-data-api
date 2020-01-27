using System;
using Jobtech.OpenPlatforms.GigDataApi.Common;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models
{
    public class ReviewDataFetchResult
    {
        public ReviewDataFetchResult(string reviewIdentifier, DateTimeOffset? reviewDate, Guid? ratingIdentifier,
            string reviewHeading, string reviewText, string reviewerName, string reviewerAvatarUri)
        {
            ReviewIdentifier = reviewIdentifier;
            ReviewDate = reviewDate;
            RatingIdentifier = ratingIdentifier;
            ReviewHeading = reviewHeading;
            ReviewText = reviewText;
            ReviewerName = reviewerName;
            ReviewerAvatarUri = reviewerAvatarUri;
        }

        public string ReviewIdentifier { get; private set; }
        public DateTimeOffset? ReviewDate { get; private set; }
        public Guid? RatingIdentifier { get; private set; }
        public string ReviewHeading { get; private set; }
        public string ReviewText { get; private set; }
        public string ReviewerName { get; private set; }
        public string ReviewerAvatarUri { get; private set; }
    }

    public class AchievementFetchResult
    {
        public AchievementFetchResult(string achievementId, string name, string achievementPlatformType,
            PlatformAchievementType achievementType, string description, string imageUri,
            AchievementScoreFetchResult score)
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
        public AchievementScoreFetchResult Score { get; private set; }
    }

    public class AchievementScoreFetchResult
    {
        public AchievementScoreFetchResult(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; private set; }
        public string Label { get; private set; }
    }
}