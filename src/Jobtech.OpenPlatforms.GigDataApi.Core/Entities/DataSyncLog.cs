using System;
using System.Collections.Generic;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class DataSyncLog
    {
        private DataSyncLog()
        {
            ExternalId = Guid.NewGuid();
            Steps = new List<DataSyncStep>();
        }

        public DataSyncLog(string userId, string platformId): this()
        {
            UserId = userId;
            PlatformId = platformId;
        }

        public string Id { get; private set; }
        public Guid ExternalId { get; private set; }
        public string UserId { get; private set; }
        public string PlatformId { get; private set; }
        public IList<DataSyncStep> Steps { get; private set; }
    }

    public class DataSyncStep
    {
        private DataSyncStep()
        {
            Created = DateTimeOffset.UtcNow;
        }

        public DataSyncStep(DataSyncStepType type, DataSyncStepState state,
            string logMessage = null, string appId = null, string appWebHookUrl = null): this()
        {
            Type = type;
            State = state;
            LogMessage = logMessage;
            AppId = appId;
            AppWebhookUrl = appWebHookUrl;
        }

        public DateTimeOffset Created { get; private set; }
        public DataSyncStepType Type { get; private set; }
        public DataSyncStepState State { get; private set; }
        public string AppId { get; private set; }
        public string AppWebhookUrl { get; private set; }
        public string LogMessage { get; private set; }

    }

    public enum DataSyncStepType
    {
        PlatformDataFetch,
        AppNotification,
        RemovePlatformConnection
    }

    public enum DataSyncStepState
    {
        Started,
        Succeeded,
        Failed,
    }
}
