using System;
using System.Collections.Generic;
using System.Linq;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class PlatformData : BaseEntity
    {
        private PlatformData() : base()
        {
            Reviews = new List<ReviewData>();
            Ratings = new List<Rating>();
            DataLog = new List<RawData>();
            LastUpdated = DateTimeOffset.UtcNow;
        }

        public PlatformData(string platformId, string userId) : this()
        {
            PlatformId = platformId;
            UserId = userId;
        }

        public string PlatformId { get; private set; }
        public string UserId { get; private set; }
        public int NumberOfGigs { get; set; }
        public DateTimeOffset? PeriodStart { get; set; }
        public DateTimeOffset? PeriodEnd { get; set; }
        public Rating AverageRating { get; set; }
        public IEnumerable<Rating> Ratings { get; set; }
        public IEnumerable<ReviewData> Reviews { get; set; }
        public IEnumerable<Achievement> Achievements { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public IReadOnlyList<RawData> DataLog { get; private set; }

        public void AddRawDataToDataLog(RawData rawData)
        {
            DataLog = new List<RawData>(DataLog) { rawData }.AsReadOnly();
            //keep only the 5 most recent
            if (DataLog.Count > 5)
            {
                DataLog = DataLog.OrderByDescending(dl => dl.Created).Take(5).ToList().AsReadOnly();
            }
        }
    }

    public class ReviewData
    {
        private ReviewData()
        {
            LastUpdated = Created = DateTimeOffset.UtcNow;
        }

        public ReviewData(string reviewIdentifier, string reviewText, string reviewHeading, string reviewerName, string reviewerAvatarUri, DateTimeOffset? reviewDate = null,
            Guid? ratingId = null): this()
        {
            ReviewIdentifier = reviewIdentifier;
            ReviewText = reviewText;
            ReviewHeading = reviewHeading;
            ReviewerName = reviewerName;
            ReviewerAvatarUri = reviewerAvatarUri;
            ReviewDate = reviewDate;
            RatingId = ratingId;
        }

        public string ReviewIdentifier { get; private set; }
        public DateTimeOffset? ReviewDate { get; private set; }
        public Guid? RatingId { get; set; }
        public string ReviewHeading { get; set; }
        public string ReviewText { get; set; }
        public string ReviewerName { get; set; }
        public string ReviewerAvatarUri { get; set; }
        public DateTimeOffset Created { get; private set; }
        public DateTimeOffset LastUpdated { get; set; }
    }

    public class RawData
    {
        private RawData()
        {
            Created = DateTimeOffset.UtcNow;
        }

        public RawData(string data) : this()
        {
            Data = data;
        }

        public DateTimeOffset Created { get; private set; }
        public string Data { get; private set; }
    }

    public class Rating
    {
        private Rating()
        {
            Identifier = Guid.NewGuid();
            Created = DateTimeOffset.UtcNow;
        }

        public Rating(Guid identifier, decimal value, decimal min, decimal max, decimal successLimit) : this()
        {
            Identifier = identifier;
            Value = value;
            Min = min;
            Max = max;
            SuccessLimit = successLimit;
        }

        public Guid Identifier { get; private set; }
        public decimal Min { get; private set; }
        public decimal Max { get; private set; }
        public decimal SuccessLimit { get; private set; }
        public decimal Value { get; private set; } 
        public DateTimeOffset Created { get; private set; }
    }

    public class Achievement
    {
        public Achievement(string achievementIdentifier, string name, string achievementPlatformType,
            PlatformAchievementType achievementType, string description, string imageUri,
            AchievementScore score)
        {
            AchievementIdentifier = achievementIdentifier;
            Name = name;
            AchievementPlatformType = achievementPlatformType;
            AchievementType = achievementType;
            Description = description;
            ImageUri = imageUri;
            Score = score;
        }

        public string AchievementIdentifier { get; private set; }
        public string Name { get; private set; }
        public string AchievementPlatformType { get; private set; }
        public PlatformAchievementType AchievementType { get; private set; }
        public string Description { get; private set; }
        public string ImageUri { get; private set; }
        public AchievementScore Score { get; private set; }
    }

    public class AchievementScore
    {
        public AchievementScore(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; private set; }
        public string Label { get; private set; }
    }
}
