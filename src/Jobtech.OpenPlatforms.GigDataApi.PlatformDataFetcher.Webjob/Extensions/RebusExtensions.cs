using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Extensions
{
    public static class RebusExtensions
    {
        public static async Task DeferMessageLocal(this IBus bus, object message, int deferForSeconds,
            Dictionary<string, string> messageHeaders = null, int? maxRetries = null, string errorQueue = null,
            ILogger logger = null)
        {
            if (messageHeaders == null)
            {
                messageHeaders = new Dictionary<string, string>();
            }

            var numberOfRetries = GetNumberOfRetries(messageHeaders);

            logger?.LogInformation("Will defer message (local). Have retried message {NoOfRetries} times so far.", numberOfRetries);

            if (maxRetries.HasValue && numberOfRetries >= maxRetries)
            {
                
                if (!string.IsNullOrEmpty(errorQueue))
                {
                    logger?.LogWarning("Number of retries exceeded max retries. Will move message to queue: {QueueName}.", errorQueue);
                    await bus.Advanced.TransportMessage.Forward(errorQueue, messageHeaders);
                }
                else
                {
                    logger?.LogWarning("Number of retries exceeded max retries. Will ignore message.");
                }

                return;
            }

            logger?.LogInformation("Will defer message for {DeferForSeconds} seconds.", deferForSeconds);

            numberOfRetries++;
            SetNumberOfRetries(messageHeaders, numberOfRetries);

            await bus.DeferLocal(new TimeSpan(0, 0, deferForSeconds), message, messageHeaders);
        }

        public static async Task DeferMessageLocalWithExponentialBackOff(this IBus bus, object message,
            Dictionary<string, string> messageHeaders = null, int? maxRetries = null, string errorQueue = null,
            int maxDelayInSeconds = 1024, ILogger logger = null)
        {
            if (messageHeaders == null)
            {
                messageHeaders = new Dictionary<string, string>();
            }

            var numberOfRetries = GetNumberOfRetries(messageHeaders);

            logger?.LogInformation("Will defer message with exponential back off (local). Have retried message {NoOfRetries} times so far.", numberOfRetries);

            if (maxRetries.HasValue && numberOfRetries >= maxRetries)
            {
                if (!string.IsNullOrEmpty(errorQueue))
                {
                    logger?.LogWarning("Number of retries exceeded max retries. Will move message to queue: {QueueName}.", errorQueue);
                    await bus.Advanced.TransportMessage.Forward(errorQueue, messageHeaders);
                }
                else
                {
                    logger?.LogWarning("Number of retries exceeded max retries. Will ignore message.");
                }

                return;
            }

            var deferForSeconds = ExponentialDelay(numberOfRetries, maxDelayInSeconds);

            logger?.LogInformation("Will defer message for {DeferForSeconds} seconds.", deferForSeconds);

            numberOfRetries++;
            SetNumberOfRetries(messageHeaders, numberOfRetries);

            await bus.DeferLocal(new TimeSpan(0, 0, deferForSeconds), message, messageHeaders);
        }

        const string RetriesHeaderName = "openplatform-numberOfRetries";

        private static int GetNumberOfRetries(IDictionary<string, string> messageHeaders)
        {
            
            if (!messageHeaders.TryGetValue(RetriesHeaderName, out var numberOfRetriesStr)) numberOfRetriesStr = "0";
            if (!int.TryParse(numberOfRetriesStr, out var numberOfRetries)) numberOfRetries = 0;

            return numberOfRetries;
        }

        private static void SetNumberOfRetries(IDictionary<string, string> messageHeaders, int numberOfRetries)
        {
            if (!messageHeaders.ContainsKey(RetriesHeaderName))
            {
                messageHeaders.Add(RetriesHeaderName, numberOfRetries.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                messageHeaders[RetriesHeaderName] = numberOfRetries.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static int ExponentialDelay(int failedAttempts,
            int maxDelayInSeconds)
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
                ? Convert.ToInt32(maxDelayInSeconds)
                : Convert.ToInt32(delayInSeconds);
        }
    }
}
