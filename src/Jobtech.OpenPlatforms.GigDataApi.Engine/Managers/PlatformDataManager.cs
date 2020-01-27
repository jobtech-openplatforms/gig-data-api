namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IPlatformDataManager
    {
        Task<PlatformData> GetPlatformData(string userId, string platformId, IAsyncDocumentSession session);
        Task RemovePlatformDataForPlatform(string userId, string platformId, IAsyncDocumentSession session);

        Task<PlatformData> AddPlatformData(string userId, string platformId, int numberOfGigs,
            DateTimeOffset? periodStart, DateTimeOffset? periodEnd, IList<RatingDataFetchResult> ratings, RatingDataFetchResult averageRating,
            IList<ReviewDataFetchResult> reviews, IList<AchievementFetchResult> achievements, string rawData, IAsyncDocumentSession session);
    }

    public class PlatformDataManager: IPlatformDataManager
    {
        private readonly IPlatformManager _platformManager;

        public PlatformDataManager(IPlatformManager platformManager)
        {
            _platformManager = platformManager;
        }

        public async Task<PlatformData> GetPlatformData(string userId, string platformId, IAsyncDocumentSession session)
        {
            return await session.Query<PlatformData>()
                .SingleOrDefaultAsync(pd => pd.UserId == userId && pd.PlatformId == platformId);
        }

        public async Task RemovePlatformDataForPlatform(string userId, string platformId, IAsyncDocumentSession session)
        {
            var existingPlatformData = await GetPlatformData(userId, platformId, session);

            session.Delete(existingPlatformData.Id);
        }

        public async Task<PlatformData> AddPlatformData(string userId, string platformId, int numberOfGigs,
            DateTimeOffset? periodStart, DateTimeOffset? periodEnd, IList<RatingDataFetchResult> ratings, RatingDataFetchResult averageRating,
            IList<ReviewDataFetchResult> reviews, IList<AchievementFetchResult> achievements, string rawData, IAsyncDocumentSession session)
        {
            var platform = await _platformManager.GetPlatform(platformId, session);

            var platformData = await GetPlatformData(userId, platformId, session);

            if (platformData == null)
            {
                platformData = new PlatformData(platform.Id, userId);
                await session.StoreAsync(platformData);
            }

            var rawPlatformData = new RawData(rawData);
            platformData.DataLog.Add(rawPlatformData);

            platformData.LastUpdated = DateTimeOffset.UtcNow;

            platformData.NumberOfGigs = numberOfGigs;
            platformData.PeriodStart = periodStart;
            platformData.PeriodEnd = periodEnd;

            if (averageRating != null)
            {
                platformData.AverageRating = new Rating(averageRating.Identifier, averageRating.Value, platform.RatingInfo.MinRating,
                    platform.RatingInfo.MaxRating, platform.RatingInfo.SuccessLimit);
            }
            else
            {
                platformData.AverageRating = null;
            }

            var transformedRatings = ratings.Select(r =>
                new KeyValuePair<Guid, Rating>(r.Identifier,
                    new Rating(r.Identifier, r.Value, platform.RatingInfo.MinRating, platform.RatingInfo.MaxRating, platform.RatingInfo.SuccessLimit)));

            var transformedReviews = reviews.Select(r =>
            {
                var rating = transformedRatings.SingleOrDefault(kvp => kvp.Key == r.RatingIdentifier).Value;
                return new ReviewData(r.ReviewIdentifier, r.ReviewText, r.ReviewHeading, r.ReviewerName, r.ReviewerAvatarUri, r.ReviewDate,
                    rating?.Identifier);
            });

            platformData.Reviews = transformedReviews;
            platformData.Ratings = transformedRatings.Select(kvp => kvp.Value);

            var transformedAchievements = achievements.Select(a =>
            {
                AchievementScore score = null;
                if (a.Score != null)
                {
                    score = new AchievementScore(a.Score.Value, a.Score.Label);
                }

                return new Achievement(a.AchievementId, a.Name, a.AchievementPlatformType, a.AchievementType,
                    a.Description, a.ImageUri, score);
            });

            platformData.Achievements = transformedAchievements;

            return platformData;
        }
    }
}