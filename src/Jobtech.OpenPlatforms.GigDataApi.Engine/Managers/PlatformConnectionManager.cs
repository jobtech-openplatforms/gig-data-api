using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Core.OAuth;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.PlatformIntegrations.Freelancer;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents.Session;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IPlatformConnectionManager
    {
        Task<PlatformOAuthConnectionStartResult> StartConnectUserToOauthPlatform(Guid externalPlatformId, User user,
            App app,
            string oauthCallbackUrl, IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task<PlatformConnectionStartResult> ConnectUserToEmailPlatform(Guid externalPlatformId, User user, App app,
            string userPlatformEmailAddress, IAsyncDocumentSession session, bool emailIsValidated = false,
            CancellationToken cancellationToken = default);

        Task<string> CompleteConnectUserToOAuthPlatform(Guid externalPlatformId, string code, string stateStr,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        IPlatformConnectionInfo GetPlatformConnectionInfo(User user, string platformId);

    }

    public class PlatformConnectionManager : IPlatformConnectionManager
    {
        private readonly IFreelancerAuthenticator _freelancerAuthenticator;
        private readonly IPlatformManager _platformManager;
        private readonly IUserManager _userManager;
        private readonly IEmailValidatorManager _emailValidatorManager;
        private readonly IAppManager _appManager;
        private readonly IAppNotificationManager _appNotificationManager;
        private readonly ILogger<PlatformConnectionManager> _logger;

        public PlatformConnectionManager(IPlatformManager platformManager, IUserManager userManager,
            IFreelancerAuthenticator freelancerAuthenticator, IEmailValidatorManager emailValidatorManager,
            IAppManager appManager, IAppNotificationManager appNotificationManager,
            ILogger<PlatformConnectionManager> logger)
        {
            _platformManager = platformManager;
            _userManager = userManager;
            _freelancerAuthenticator = freelancerAuthenticator;
            _emailValidatorManager = emailValidatorManager;
            _appManager = appManager;
            _appNotificationManager = appNotificationManager;
            _logger = logger;
        }

        public async Task<PlatformOAuthConnectionStartResult> StartConnectUserToOauthPlatform(Guid externalPlatformId,
            User user, App app,
            string oauthCallbackUrl, IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var platform = await _platformManager.GetPlatformByExternalId(externalPlatformId, session, cancellationToken);

            if (platform.AuthenticationMechanism != PlatformAuthenticationMechanism.Oauth2)
            {
                throw new PlatformAuthMechanismMismatchException(
                    $"Platform auth mechanism must be {PlatformAuthenticationMechanism.Oauth2}");
            }

            var existingPlatformConnection =
                user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platform.Id);

            if (existingPlatformConnection != null)
            {
                //we already have a connection
                if (existingPlatformConnection.ConnectionInfo.NotificationInfos.All(ni => ni.AppId != app.Id))
                {
                    existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id));
                }

                await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id, new List<string> {app.Id},
                    platform.Id, session, cancellationToken);

                return new PlatformOAuthConnectionStartResult(PlatformConnectionState.Connected);
            }

            await _appNotificationManager.NotifyPlatformConnectionAwaitingOAuthAuthentication(user.Id,
                new List<string> {app.Id}, platform.Id, session, cancellationToken);

            switch (platform.IntegrationType)
            {
                case PlatformIntegrationType.FreelancerIntegration:
                    var oauthAuthenticationUrl =
                        _freelancerAuthenticator.GetAuthorizationUrl(user.ExternalId, oauthCallbackUrl,
                            app.ApplicationId);
                    return new PlatformOAuthConnectionStartResult(PlatformConnectionState.AwaitingOAuthAuthentication,
                        oauthAuthenticationUrl);
                case PlatformIntegrationType.AirbnbIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.UpworkIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.GigDataPlatformIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.Manual:
                    throw new PlatformDoesNotSupportAutomaticConnection();
                default:
                    throw new ArgumentException($"Unknown integration type {platform.IntegrationType}");

            }
        }

        public async Task<PlatformConnectionStartResult> ConnectUserToEmailPlatform(Guid externalPlatformId, User user,
            App app,
            string userPlatformEmailAddress, IAsyncDocumentSession session, bool emailIsValidated = false,
            CancellationToken cancellationToken = default)
        {
            userPlatformEmailAddress = userPlatformEmailAddress.ToLowerInvariant();

            var platform = await _platformManager.GetPlatformByExternalId(externalPlatformId, session);

            if (platform.AuthenticationMechanism != PlatformAuthenticationMechanism.Email)
            {
                throw new PlatformAuthMechanismMismatchException(
                    $"Platform auth mechanism must be {PlatformAuthenticationMechanism.Email}");
            }

            if (string.IsNullOrEmpty(userPlatformEmailAddress))
            {
                throw new ArgumentException(
                    $"When connecting platform that has email as authentication mechanism, an email address must be provided");
            }

            var existingPlatformConnection =
                user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platform.Id);

            if (existingPlatformConnection != null)
            {
                //we already have a connection
                if (existingPlatformConnection.ConnectionInfo.GetType() != typeof(EmailPlatformConnectionInfo) ||
                    ((EmailPlatformConnectionInfo) (existingPlatformConnection.ConnectionInfo)).Email !=
                    userPlatformEmailAddress)
                {
                    //the connection was either not an email connection (could happen if the implementation switched auth type at some point in time) or
                    //the email connected was not the same. In both cases, we should remove the existing connection and create a new one
                    var connectionIndex = Array.FindIndex(user.PlatformConnections.ToArray(),
                        pc => pc.PlatformId == platform.Id);
                    user.PlatformConnections.RemoveAt(connectionIndex);
                }
                else
                {
                    //just add the app to notification infos if it isn't already there.
                    if (existingPlatformConnection.ConnectionInfo.NotificationInfos.All(ni => ni.AppId != app.Id))
                    {
                        existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id));

                        await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id,
                            new List<string> {app.Id},
                            platform.Id, session,
                            cancellationToken);
                    }

                    return new PlatformConnectionStartResult(PlatformConnectionState.Connected);
                }
            }

            if (!emailIsValidated)
            {
                var existingUserEmail =
                    user.UserEmails.SingleOrDefault(ue => ue.Email == userPlatformEmailAddress);

                if (existingUserEmail == null || existingUserEmail.UserEmailState == UserEmailState.Unverified)
                {
                    //TODO: we need to think about how to resend verification as well

                    //user email is unverified, start verification process
                    await _emailValidatorManager.StartEmailValidation(userPlatformEmailAddress, user, app, session,
                        platform.Id, cancellationToken: cancellationToken);

                    await _appNotificationManager.NotifyPlatformConnectionAwaitingEmailVerification(user.Id,
                        new List<string> {app.Id}, platform.Id, session, cancellationToken);

                    return new PlatformConnectionStartResult(PlatformConnectionState.AwaitingEmailVerification);

                }
                else if (existingUserEmail.UserEmailState == UserEmailState.AwaitingVerification)
                {
                    //the email validation has already been started
                    return new PlatformConnectionStartResult(PlatformConnectionState.AwaitingEmailVerification);
                }
            }

            switch (platform.IntegrationType)
            {
                case PlatformIntegrationType.FreelancerIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.AirbnbIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.UpworkIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.GigDataPlatformIntegration:
                    //TODO: call AF.CVData.PlatformIntegrations.GigDataPlatform to start connection there
                    await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id, new List<string> {app.Id},
                        platform.Id, session, cancellationToken);
                    await HandleEmailValidationCompletion(platform.Id, user, app, userPlatformEmailAddress, session,
                        cancellationToken);
                    return new PlatformConnectionStartResult(PlatformConnectionState.Connected);
                case PlatformIntegrationType.Manual:
                    throw new PlatformDoesNotSupportAutomaticConnection();
                default:
                    throw new ArgumentException($"Unknown integration type {platform.IntegrationType}");

            }
        }

        public async Task<string> CompleteConnectUserToOAuthPlatform(Guid externalPlatformId, string code,
            string stateStr,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var platform =
                await _platformManager.GetPlatformByExternalId(externalPlatformId, session, cancellationToken);

            OAuthCompleteResult completeResult;
            switch (platform.IntegrationType)
            {
                case PlatformIntegrationType.FreelancerIntegration:
                    completeResult = await _freelancerAuthenticator.CompleteAuthorization(code, stateStr);
                    break;
                case PlatformIntegrationType.AirbnbIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.UpworkIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.GigDataPlatformIntegration:
                    throw new NotImplementedException();
                case PlatformIntegrationType.Manual:
                    throw new PlatformDoesNotSupportAutomaticConnection();
                default:
                    throw new ArgumentException($"Unknown integration type {platform.IntegrationType}");
            }

            return await HandleOAuthCompleteResult(completeResult, platform, session, cancellationToken);
        }

        public IPlatformConnectionInfo GetPlatformConnectionInfo(User user, string platformId)
        {
            var connection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platformId);

            if (connection == null)
            {
                throw new PlatformConnectionDoesNotExistException(
                    $"Platform connection for platform with id {platformId} does not exist for user with id {user.Id}");
            }

            return connection.ConnectionInfo;
        }

        public void UpdatePlatformConnectionInfo(User user, string platformId,
            IPlatformConnectionInfo connectionInfo)
        {
            var connection = user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platformId);

            if (connection == null)
            {
                throw new PlatformConnectionDoesNotExistException(
                    $"Platform connection for platform with id {platformId} does not exist for user with id {user.Id}");
            }

            connection.ConnectionInfo = connectionInfo;
        }

        private async Task<string> HandleOAuthCompleteResult(OAuthCompleteResult oAuthCompleteResult, Platform platform,
            IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserByExternalId(oAuthCompleteResult.UserId, session);
            var app = await _appManager.GetAppFromApplicationId(oAuthCompleteResult.ApplicationId, session,
                cancellationToken);

            var existingPlatformConnection =
                user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platform.Id);
            if (existingPlatformConnection != null)
            {
                if (existingPlatformConnection.ConnectionInfo.NotificationInfos.All(ni => ni.AppId != app.Id))
                {
                    existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id));
                }

                if (existingPlatformConnection.ConnectionInfo.GetType() != typeof(OAuthPlatformConnectionInfo))
                {
                    //we have another type of connection right now for this platform. We will replace it with an oauth connection instead.
                    _logger.LogInformation(
                        $"Had connection info of type {existingPlatformConnection.ConnectionInfo.GetType().Name}. Will replace that with OAuth connection info. UserId: {user.Id}, Platform: {platform.Name}");
                }

                var oauthPlatformConnectionInfo = new OAuthPlatformConnectionInfo(new Token(oAuthCompleteResult.Token));
                foreach (var connectionInfoNotificationInfo in existingPlatformConnection.ConnectionInfo
                    .NotificationInfos)
                {
                    oauthPlatformConnectionInfo.NotificationInfos.Add(connectionInfoNotificationInfo);
                }

                existingPlatformConnection.ConnectionInfo = oauthPlatformConnectionInfo;
            }



            var platformConnection = new PlatformConnection(platform.Id, platform.Name, platform.ExternalId,
                new OAuthPlatformConnectionInfo(new Token(oAuthCompleteResult.Token)),
                platform.DataPollIntervalInSeconds);
            platformConnection.ConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id));

            user.PlatformConnections.Add(platformConnection);

            await session.SaveChangesAsync(cancellationToken);

            await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id, new List<string> {app.Id},
                platform.Id, session, cancellationToken);

            return GetRedirectUrl(oAuthCompleteResult.RedirectUrl, platform.ExternalId);
        }

        private async Task HandleEmailValidationCompletion(string platformId, User user, App app,
            string userPlatformEmailAddress, IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var existingPlatformConnection =
                user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platformId);
            if (existingPlatformConnection != null)
            {
                //add notification info
                if (existingPlatformConnection.ConnectionInfo.NotificationInfos.All(ni => ni.AppId != app.Id))
                {
                    existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id));
                }

                if (existingPlatformConnection.ConnectionInfo.GetType() != typeof(EmailPlatformConnectionInfo))
                {
                    //we have another type of connection to this platform. We will replace it with an email-connection. Last in last served.
                    _logger.LogInformation(
                        $"Had connection info of type {existingPlatformConnection.ConnectionInfo.GetType().Name}. Will replace that with Email connection info. UserId: {user.Id}, PlatformId: {platformId}");
                }
                else if (((EmailPlatformConnectionInfo) existingPlatformConnection.ConnectionInfo).Email !=
                         userPlatformEmailAddress)
                {
                    //email has changed for this user. create a replacement connection info
                    _logger.LogInformation(
                        $"Email has changed from {((EmailPlatformConnectionInfo) existingPlatformConnection.ConnectionInfo).Email} to {userPlatformEmailAddress}. Will replace it. UserId: {user.Id}, PlatformId: {platformId}");
                    var newConnectionInfo = new EmailPlatformConnectionInfo(userPlatformEmailAddress);
                    foreach (var connectionInfoNotificationInfo in existingPlatformConnection.ConnectionInfo
                        .NotificationInfos)
                    {
                        newConnectionInfo.NotificationInfos.Add(connectionInfoNotificationInfo);
                    }

                    existingPlatformConnection.ConnectionInfo = newConnectionInfo;
                }

                return;
            }

            var platform = await _platformManager.GetPlatform(platformId, session, cancellationToken);

            var emailConnectionInfo = new EmailPlatformConnectionInfo(userPlatformEmailAddress);
            emailConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id));

            var platformConnection = new PlatformConnection(platform.Id, platform.Name, platform.ExternalId,
                emailConnectionInfo, platform.DataPollIntervalInSeconds);
            user.PlatformConnections.Add(platformConnection);
        }

        private string GetRedirectUrl(string originalRedirectUrl, Guid externalPlatformId)
        {
            if (originalRedirectUrl.Contains("?"))
            {
                //we already have query parameters
                return $"{originalRedirectUrl}&platform_id={externalPlatformId}";
            }
            else
            {
                return $"{originalRedirectUrl}?platform_id={externalPlatformId}";
            }
        }
    }

    public class PlatformConnectionStartResult
    {
        public PlatformConnectionStartResult(PlatformConnectionState state)
        {
            State = state;
        }

        public PlatformConnectionState State { get; }
    }

    public class PlatformOAuthConnectionStartResult : PlatformConnectionStartResult
    {
        public PlatformOAuthConnectionStartResult(PlatformConnectionState state, string oAuthAuthenticationUrl = null) :
            base(state)
        {
            OAuthAuthenticationUrl = oAuthAuthenticationUrl;
        }

        public string OAuthAuthenticationUrl { get; }
    }
}
