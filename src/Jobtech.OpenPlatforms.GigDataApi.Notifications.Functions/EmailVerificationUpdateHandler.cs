using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Jobtech.OpenPlatforms.GigDataApi.Notifications.Functions
{
    public static class EmailValidationDone
    {
        [FunctionName("EmailVerificationUpdateHandler")]
        [return: ServiceBus("emailverification.update", Connection = "ServiceBusConnectionString")]
        public static async Task<Message> Run([ServiceBusTrigger("emailverification.update", Connection = "ServiceBusConnectionString")]Message message, string lockToken, MessageReceiver messageReceiver, ILogger log)
        {
            var body = System.Text.Encoding.UTF8.GetString(message.Body);

            var mailValidationMessage =
                JsonConvert.DeserializeObject<EmailVerificationNotificationMessage>(body);

            if (string.IsNullOrWhiteSpace(mailValidationMessage.NotificationEndpoint))
            {
                log.LogWarning(
                    $"No notification endpoint was given for notifying app with shared secret {mailValidationMessage.SharedSecret} where to be notified for an email verification for user {mailValidationMessage.UserId} and email address {mailValidationMessage.Email}");
                return null;
            }

            var payload = new MailVerificationNotificationPayload
            {
                UserId = mailValidationMessage.UserId,
                Email = mailValidationMessage.Email,
                Verified = mailValidationMessage.WasVerified,
                SharedSecret = mailValidationMessage.SharedSecret
            };

            var httpClient = new HttpClient {BaseAddress = new Uri(mailValidationMessage.NotificationEndpoint)};

            try
            {
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                var serializedPayload = JsonConvert.SerializeObject(payload, serializerSettings);
                log.LogInformation($"Payload to be sent to {mailValidationMessage.NotificationEndpoint}: {serializedPayload}");

                var content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await httpClient.PostAsync(
                    mailValidationMessage.NotificationEndpoint,
                    content);
                if (!response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    log.LogError($"Got non 200 status code ({response.StatusCode}). Got response: '{responseText}'. Will defer message and try again later.");
                    var newMessage = MessageUtility.GetDeferredMessage(message, body, 10, log);
                    await messageReceiver.RenewLockAsync(message);
                    await messageReceiver.CompleteAsync(lockToken);
                    return newMessage;
                }
            }
            catch (Exception e)
            {
                log.LogError(e, $"Got exception when calling endpoint {mailValidationMessage.NotificationEndpoint}");
                var newMessage = MessageUtility.GetDeferredMessage(message, body, 10, log);
                await messageReceiver.CompleteAsync(lockToken);
                return newMessage;
            }

            log.LogInformation($"Call to endpoint {mailValidationMessage.NotificationEndpoint} succeeded");

            return null;
        }


    }

    public class MailVerificationNotificationPayload
    {
        public Guid UserId { get; set; }
        public string SharedSecret { get; set; }
        public string Email { get; set; }
        public bool Verified { get; set; }
    }
}
