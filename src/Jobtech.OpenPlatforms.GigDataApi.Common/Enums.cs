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
}
