using System;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class Platform : BaseEntity
    {
        private Platform(): base()
        {
        }

        public Platform(string name, Guid externalId, PlatformAuthenticationMechanism authMechanism,
            PlatformIntegrationType integrationType, RatingInfo ratingInfo, string description, string logoUrl,
            string websiteUrl, int? dataPollIntervalInSeconds = null, bool isInactive = false) : this()
        {
            Name = name;
            ExternalId = externalId;
            AuthenticationMechanism = authMechanism;
            IntegrationType = integrationType;
            RatingInfo = ratingInfo;
            Description = description;
            LogoUrl = logoUrl;
            WebsiteUrl = websiteUrl;
            DataPollIntervalInSeconds = dataPollIntervalInSeconds;
            IsInactive = isInactive;
        }

        public string Name { get; set; }
        public Guid ExternalId { get; private set; }
        public PlatformIntegrationType IntegrationType { get; private set; }
        public PlatformAuthenticationMechanism AuthenticationMechanism { get; private set; }
        public int? DataPollIntervalInSeconds { get; private set; }
        public RatingInfo RatingInfo { get; private set; }
        public bool IsInactive { get; set; }
        public string Description { get; set; }
        public string LogoUrl { get; set; }
        public string WebsiteUrl { get; set; }
    }

    public class RatingInfo
    {
        private RatingInfo() { }

        public RatingInfo(decimal minRating, decimal maxRating, decimal successLimit)
        {
            MinRating = minRating;
            MaxRating = maxRating;
            SuccessLimit = successLimit;
        }

        public decimal MinRating { get; private set; }
        public decimal MaxRating { get; private set; }
        public decimal SuccessLimit { get; private set; }
    }

    public enum PlatformIntegrationType {
        FreelancerIntegration,
        UpworkIntegration,
        AirbnbIntegration,
        GigDataPlatformIntegration,
        Manual
    }
}
