using System;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Jobtech.OpenPlatforms.GigDataApi.Notifications.Functions
{
    public static class DataFetchSchedulerTrigger
    {
        [FunctionName("PlatformDataFetcherSchedulerTrigger")]
        [return: ServiceBus("platformdatafetcher.input", Connection = "ServiceBusConnectionString")]
        public static Message Run([TimerTrigger("0 */5 * * * *", RunOnStartup = true)]TimerInfo myTimer, ILogger log)
        {
            var message = new Message
            {
                Body = System.Text.Encoding.UTF8.GetBytes("{}"),
                UserProperties = {
                    ["rbs2-msg-type"] = typeof(PlatformDataFetcherTriggerMessage).AssemblyQualifiedName,
                    ["rbs2-msg-id"] = Guid.NewGuid(),
                    ["rbs2-content-type"] = "application/json;charset=utf-8"
                }
            };

            log.LogInformation("Will send PlatformDataFetcherTriggerMessage");
            return message;
        }
    }
}
