namespace Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.GigPlatform.Models
{
    public class RequestLatestResult
    {
        public RequestLatestResult(string requestId, string message, bool success)
        {
            RequestId = requestId;
            Message = message;
            Success = success;
        }

        public string RequestId { get; private set; }
        public string Message { get; private set; }
        public bool Success { get; private set; }
    }
}
