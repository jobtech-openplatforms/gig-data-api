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
}
