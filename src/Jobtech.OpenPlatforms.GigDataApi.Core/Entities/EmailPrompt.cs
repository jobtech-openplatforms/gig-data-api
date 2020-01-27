using System;
using System.Collections.Generic;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class EmailPrompt: BaseEntity
    {
        private EmailPrompt(): base() { }

        public EmailPrompt(string promptId, string userId, string emailAddress, int expiresAt, string appId, string platformId)
        {
            PromptId = promptId;
            UserId = userId;
            EmailAddress = emailAddress;
            ExpiresAt = expiresAt;
            PlatformIdToAppId = new Dictionary<string, IList<string>> {{platformId, new List<string> {appId}}};
        }


        public string PromptId { get; private set; }
        public string UserId { get; private set; }
        public string EmailAddress { get; private set; }
        public int ExpiresAt { get; private set; }
        public bool ExpiredManually { get; private set; }
        public IDictionary<string, IList<string>> PlatformIdToAppId { get; private set; }

        public bool? Result { get; private set; }

        public void SetResult(bool result)
        {
            MarkAsExpired();
            Result = result;
        }

        public bool HasExpired()
        {
            var unixTimestampNow = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            return unixTimestampNow > ExpiresAt;
        }

        public void MarkAsExpired()
        {
            ExpiredManually = true;
            var unixTimestampNow = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            ExpiresAt = (int)unixTimestampNow;
        }

    }
}
