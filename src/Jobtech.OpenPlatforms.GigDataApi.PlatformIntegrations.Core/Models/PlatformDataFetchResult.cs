using System;
using System.Collections.Generic;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Core.Models
{
    public class PlatformDataFetchResult
    {
        public PlatformDataFetchResult(int numberOfGigs, DateTimeOffset? periodStart,
            DateTimeOffset? periodEnd, RatingDataFetchResult averageRating, IList<RatingDataFetchResult> ratings, 
            IList<ReviewDataFetchResult> reviews, IList<AchievementFetchResult> achievements, string rawData)
        {
            NumberOfGigs = numberOfGigs;
            PeriodStart = periodStart;
            PeriodEnd = periodEnd;
            AverageRating = averageRating;
            Ratings = ratings;
            Reviews = reviews;
            Achievements = achievements;
            RawData = rawData;
        }

        public int NumberOfGigs { get; private set; }
        public DateTimeOffset? PeriodStart { get; private set; }
        public DateTimeOffset? PeriodEnd { get; private set; }
        public IList<RatingDataFetchResult> Ratings { get; private set; }
        public RatingDataFetchResult AverageRating { get; private set; }
        public IList<ReviewDataFetchResult> Reviews { get; private set; }
        public IList<AchievementFetchResult> Achievements { get; private set; }
        public string RawData { get; private set; }
    }
}
