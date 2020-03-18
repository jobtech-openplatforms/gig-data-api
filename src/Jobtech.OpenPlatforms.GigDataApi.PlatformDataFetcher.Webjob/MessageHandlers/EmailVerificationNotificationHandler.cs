using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common.Extensions;
using Jobtech.OpenPlatforms.GigDataApi.Common.Messages;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Raven.Client.Documents;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Retry.Simple;

namespace Jobtech.OpenPlatforms.GigDataApi.PlatformDataFetcher.Webjob.MessageHandlers
{
    public class EmailVerificationNotificationHandler: IHandleMessages<EmailVerificationNotificationMessage>, IHandleMessages<IFailed<EmailVerificationNotificationMessage>>
    {
        private readonly IAppManager _appManager;
        private readonly IDocumentStore _documentStore;
        private readonly RebusConfiguration _rebusConfiguration;
        private readonly IBus _bus;
        private readonly IMessageContext _messageContext;
        private readonly ILogger<EmailVerificationNotificationHandler> _logger;

        private const int MaxMessageRetries = 100;

        public EmailVerificationNotificationHandler(IAppManager appManager, IDocumentStore documentStore,
            IOptions<RebusConfiguration> rebusOptions, IBus bus, IMessageContext messageContext,
            ILogger<EmailVerificationNotificationHandler> logger)
        {
            _appManager = appManager;
            _documentStore = documentStore;
            _rebusConfiguration = rebusOptions.Value;
            _bus = bus;
            _messageContext = messageContext;
            _logger = logger;
        }

        public async Task Handle(EmailVerificationNotificationMessage message)
        {
            using var loggerScope = _logger.BeginNamedScopeWithMessage(nameof(DataFetchCompleteHandler),
                _messageContext.Message.GetMessageId(),
                (LoggerPropertyNames.ApplicationId, message.AppId),
                (LoggerPropertyNames.UserId, message.UserId));

            using var session = _documentStore.OpenAsyncSession();

            var cancellationToken = _messageContext.GetCancellationToken();

            var user = await session.LoadAsync<User>(message.UserId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning(
                    $"User with given id does not exist. Will move message to error queue.");
                await _bus.Advanced.TransportMessage.Forward(_rebusConfiguration.ErrorQueueName);
                return;
            }

            var wasVerified = user.UserEmails.SingleOrDefault(ue => ue.Email == message.Email)?.IsVerifiedFromApp ?? false;

            var app = await _appManager.GetAppFromId(message.AppId, session, cancellationToken);

            if (app == null)
            {
                _logger.LogWarning(
                    $"App with given id does not exist. Will move message to error queue.");
                await _bus.Advanced.TransportMessage.Forward(_rebusConfiguration.ErrorQueueName);
                return;
            }

            var notificationEndpoint = app.EmailVerificationNotificationEndpoint;
            var sharedSecret = app.SecretKey;

            using var innerLoggerScope = _logger.BeginPropertyScope(
                (LoggerPropertyNames.NotificationEndpoint, notificationEndpoint),
                (LoggerPropertyNames.NotificationEndpointSharedSecret, sharedSecret));

            if (string.IsNullOrWhiteSpace(notificationEndpoint))
            {
                _logger.LogWarning(
                    $"No notification endpoint was given. Will move message to error queue.");
                await _bus.Advanced.TransportMessage.Forward(_rebusConfiguration.ErrorQueueName);
                return;
            }

            var payload = new MailVerificationNotificationPayload
            {
                UserId = user.ExternalId,
                Email = message.Email,
                Verified = wasVerified,
                SharedSecret = sharedSecret
            };

            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var serializedPayload = JsonConvert.SerializeObject(payload, serializerSettings);
            _logger.LogTrace("Payload to be sent: {Payload}", serializedPayload);

            var content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;

            try
            {
                var httpClient = new HttpClient();
                response = await httpClient.PostAsync(
                    new Uri(notificationEndpoint), 
                    content, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Got error calling endpoint. Will schedule retry.");
                await _bus.DeferMessageLocalWithExponentialBackOff(message, _messageContext.Headers, MaxMessageRetries,
                    _rebusConfiguration.ErrorQueueName, logger: _logger);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Got non success status code ({HttpStatusCode}) calling endpoint. Will schedule retry.", response.StatusCode);
                await _bus.DeferMessageLocalWithExponentialBackOff(message, _messageContext.Headers, MaxMessageRetries,
                    _rebusConfiguration.ErrorQueueName, logger: _logger);
                return;
            }

            _logger.LogInformation("App successfully notified about email verification.");
        }

        public Task Handle(IFailed<EmailVerificationNotificationMessage> message)
        {
            throw new NotImplementedException();
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
