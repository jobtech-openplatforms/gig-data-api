namespace Jobtech.OpenPlatforms.GigDataApi.Common
{
    public enum PlatformAchievementType
    {
        QualificationAssessment,
        Badge
    }

    public enum PlatformConnectionState
    {
        AwaitingOAuthAuthentication,
        AwaitingEmailVerification,
        Connected,
        Synced,
        Removed
    }

    public enum PlatformDataClaim
    {
        Aggregated,
        Full
    }

    public enum PlatformIntegrationType
    {
        FreelancerIntegration,
        UpworkIntegration,
        AirbnbIntegration,
        GigDataPlatformIntegration,
        Manual
    }

    public enum PlatformConnectionDeleteReason
    {
        /// <summary>
        /// For email platforms, when we get a 404 as reply
        /// </summary>
        UserDidNotExist,
        /// <summary>
        /// For oauth platforms, when we cannot authorize with the given token info
        /// </summary>
        NotAuthorized
    }
}
