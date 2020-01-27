using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Jobtech.OpenPlatforms.GigDataApi.Notifications.Functions
{
    public static class MessageUtility
    {
        public static Message GetDeferredMessage(Message message, string body, int maxNumberOfRetries = 10, ILogger logger = null)
        {
            var numberOfAttempts = message.UserProperties.ContainsKey("number-of-retries")
                ? (int)message.UserProperties["number-of-retries"]
                : 1;

            var originalMessageId = message.UserProperties.ContainsKey("original-message-id")
                ? message.UserProperties["original-message-id"]
                : message.MessageId;

            logger?.LogInformation($"GetDeferredMessage - Message have had {numberOfAttempts} retries. MessageId: {message.MessageId}, OriginalMessageId: {originalMessageId}");

            if (numberOfAttempts >= maxNumberOfRetries)
            {
                logger?.LogInformation($"GetDeferredMessage - Message had more than {maxNumberOfRetries}. Will not create new message. MessageId: {message.MessageId}, OriginalMessageId: {originalMessageId}");
                return null;
            }

            var delayInSeconds = ExponentialDelay(numberOfAttempts);

            var scheduledTime = DateTime.UtcNow.AddSeconds(delayInSeconds);

            var newMessage = new Message
            {
                Label = message.Label,
                Body = System.Text.Encoding.UTF8.GetBytes(body),
                UserProperties = {
                    ["number-of-retries"] = ++numberOfAttempts,
                    ["original-message-id"] = originalMessageId
                },
                ScheduledEnqueueTimeUtc = scheduledTime
            };

            logger?.LogInformation($"Will defer message for {delayInSeconds} seconds (ScheduledEnqueueTimeUtc: {scheduledTime}). MessageId: {message.MessageId}, OriginalMessageId: {originalMessageId}");

            //return new Dictionary<string, object>
            //{
            //    {
            //        "ScheduledEnqueueTimeUtc", scheduledTime
            //    },
            //    {
            //        "UserProperties", new Dictionary<string, object> {{"number-of-retries", ++numberOfAttempts}}
            //    }

            //};

            return newMessage;
        }

        private static long ExponentialDelay(int failedAttempts,
            int maxDelayInSeconds = 1024)
        {
            //Attempt 1     0s     0s
            //Attempt 2     2s     2s
            //Attempt 3     4s     4s
            //Attempt 4     8s     8s
            //Attempt 5     16s    16s
            //Attempt 6     32s    32s

            //Attempt 7     64s     1m 4s
            //Attempt 8     128s    2m 8s
            //Attempt 9     256s    4m 16s
            //Attempt 10    512     8m 32s
            //Attempt 11    1024    17m 4s
            //Attempt 12    2048    34m 8s

            //Attempt 13    4096    1h 8m 16s
            //Attempt 14    8192    2h 16m 32s
            //Attempt 15    16384   4h 33m 4s

            var delayInSeconds = ((1d / 2d) * (Math.Pow(2d, failedAttempts) - 1d));

            return maxDelayInSeconds < delayInSeconds
                ? Convert.ToInt64(maxDelayInSeconds)
                : Convert.ToInt64(delayInSeconds);
        }
    }
}