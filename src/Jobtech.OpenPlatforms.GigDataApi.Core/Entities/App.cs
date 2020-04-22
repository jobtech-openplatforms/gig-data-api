using System;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class App : BaseEntity
    {
        private App()
        {
        }

        public App(string name, Guid externalId, string secretKey, string dataUpdateCallbackUrl,
            string authorizationCallbackUrl, string description, string logoUrl, string websiteUrl,
            bool isInactive = false) : this()
        {
            Name = name;
            ExternalId = externalId;
            SecretKey = secretKey;
            DataUpdateCallbackUrl = dataUpdateCallbackUrl;
            AuthorizationCallbackUrl = authorizationCallbackUrl;
            Description = description;
            LogoUrl = logoUrl;
            WebsiteUrl = websiteUrl;
            IsInactive = isInactive;
        }

        public string Name { get; set; }
        public string DataUpdateCallbackUrl { get; set; }
        public string AuthorizationCallbackUrl { get; set; }
        public string SecretKey { get; set; }
        public Guid ExternalId { get; private set; }
        public bool IsInactive { get; set; }
        public string Description { get; set; }
        public string LogoUrl { get; set; }
        public string WebsiteUrl { get; set; }
    }
}
