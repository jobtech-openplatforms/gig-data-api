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
        Task<(PlatformOAuthConnectionStartResult PlatformConnectionStartResult, PlatformConnection PlatformConnection, PlatformIntegrationType PlatformIntegrationType)>
            StartConnectUserToOauthPlatform(Guid externalPlatformId, User user, App app, PlatformDataClaim? platformDataClaim,
                string oauthCallbackUrl, IAsyncDocumentSession session, CancellationToken cancellationToken = default);

        Task<(PlatformConnectionStartResult PlatformConnectionStartResult, PlatformConnection PlatformConnection,
            PlatformIntegrationType PlatformIntegrationType)>
            ConnectUserToEmailPlatform(Guid externalPlatformId, User user,
            App app, string userPlatformEmailAddress, string emailVerificationAcceptUrl, string emailVerificationDeclineUrl,
            PlatformDataClaim? platformDataClaim, IAsyncDocumentSession session, bool emailIsValidated = false,
            CancellationToken cancellationToken = default);

        Task<(string RedirectUrl, PlatformConnection PlatformConnection, string UserId, PlatformIntegrationType PlatformIntegrationType)>
            CompleteConnectUserToOAuthPlatform(Guid externalPlatformId, string code,
            string stateStr,
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

        public async Task<(PlatformOAuthConnectionStartResult PlatformConnectionStartResult, PlatformConnection PlatformConnection, PlatformIntegrationType PlatformIntegrationType)> 
            StartConnectUserToOauthPlatform(Guid externalPlatformId, User user, App app, PlatformDataClaim? platformDataClaim,
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
                //we already have a connection to the platform

                if (existingPlatformConnection.ConnectionInfo.GetType() != typeof(OAuthPlatformConnectionInfo))
                {
                    //the connection was of another type (Email). Remove it and replace it with an oauth connection.
                    var connectionIndex = Array.FindIndex(user.PlatformConnections.ToArray(),
                        pc => pc.PlatformId == platform.Id);
                    user.PlatformConnections.RemoveAt(connectionIndex);
                }
                else
                {
                    var existingNotificationInfo =
                        existingPlatformConnection.ConnectionInfo.NotificationInfos.SingleOrDefault(
                            ni => ni.AppId == app.Id);

                    if (existingNotificationInfo == null)
                    {
                        existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(
                            new NotificationInfo(app.Id, platformDataClaim ?? app.DefaultPlatformDataClaim));
                    }
                    else
                    {
                        //there already existed a notification info to the app at hand. Make sure that it has the given PlatformDataClaim.
                        existingNotificationInfo.PlatformDataClaim = platformDataClaim ?? app.DefaultPlatformDataClaim;
                    }

                    await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id, new List<string> { app.Id },
                        platform.Id, session, cancellationToken);

                    return (new PlatformOAuthConnectionStartResult(PlatformConnectionState.Connected), existingPlatformConnection, platform.IntegrationType);
                }
            }

            await _appNotificationManager.NotifyPlatformConnectionAwaitingOAuthAuthentication(user.Id,
                new List<string> {app.Id}, platform.Id, session, cancellationToken);

            switch (platform.IntegrationType)
            {
                case PlatformIntegrationType.FreelancerIntegration:
                    var oauthAuthenticationUrl =
                        _freelancerAuthenticator.GetAuthorizationUrl(user.ExternalId, oauthCallbackUrl,
                            app.ExternalId, platformDataClaim);
                    return (new PlatformOAuthConnectionStartResult(PlatformConnectionState.AwaitingOAuthAuthentication,
                        oauthAuthenticationUrl), null, platform.IntegrationType);
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

        public async Task<(PlatformConnectionStartResult PlatformConnectionStartResult, PlatformConnection PlatformConnection, 
            PlatformIntegrationType PlatformIntegrationType)> 
            ConnectUserToEmailPlatform(Guid externalPlatformId, User user,
            App app, string userPlatformEmailAddress, string emailVerificationAcceptUrl, string emailVerificationDeclineUrl,
            PlatformDataClaim? platformDataClaim, IAsyncDocumentSession session, bool emailIsValidated = false,
            CancellationToken cancellationToken = default)
        {
            userPlatformEmailAddress = userPlatformEmailAddress.ToLowerInvariant();

            var platform = await _platformManager.GetPlatformByExternalId(externalPlatformId, session, cancellationToken);

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
                    var existingNotificationInfo =
                        existingPlatformConnection.ConnectionInfo.NotificationInfos.SingleOrDefault(ni =>
                            ni.AppId == app.Id);

                    //just add the app to notification infos if it isn't already there.
                    if (existingNotificationInfo == null)
                    {
                        existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(
                            new NotificationInfo(app.Id, platformDataClaim ?? app.DefaultPlatformDataClaim));

                        await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id,
                            new List<string> {app.Id},
                            platform.Id, session,
                            cancellationToken);
                    }
                    else //make sure that we have the correct PlatformDataClaim
                    {
                        existingNotificationInfo.PlatformDataClaim = platformDataClaim ?? app.DefaultPlatformDataClaim;
                    }

                    return (new PlatformConnectionStartResult(PlatformConnectionState.Connected), existingPlatformConnection, platform.IntegrationType);
                }
            }

            if (!emailIsValidated)
            {
                var existingUserEmail =
                    user.UserEmails.SingleOrDefault(ue => ue.Email == userPlatformEmailAddress);

                if (existingUserEmail == null || 
                    existingUserEmail.UserEmailState == UserEmailState.Unverified)
                {
                    //user email is unverified, start verification process
                    await _emailValidatorManager.StartEmailValidation(userPlatformEmailAddress, user, app, platformDataClaim,
                        emailVerificationAcceptUrl, emailVerificationDeclineUrl, session,
                        platform.Id, cancellationToken: cancellationToken);

                    await _appNotificationManager.NotifyPlatformConnectionAwaitingEmailVerification(user.Id,
                        new List<string> {app.Id}, platform.Id, session, cancellationToken);

                    return (new PlatformConnectionStartResult(PlatformConnectionState.AwaitingEmailVerification), existingPlatformConnection, platform.IntegrationType);

                }
                
                if (existingUserEmail.UserEmailState == UserEmailState.AwaitingVerification)
                {
                    //another email validation has already been started
                    //invalidate that email validation and start a new one

                    await _emailValidatorManager.ExpireAllActiveEmailValidationsForUserEmail(userPlatformEmailAddress, user,
                        session, cancellationToken);

                    await _emailValidatorManager.StartEmailValidation(userPlatformEmailAddress, user, app, platformDataClaim,
                        emailVerificationAcceptUrl, emailVerificationDeclineUrl, session,
                        platform.Id, cancellationToken: cancellationToken);

                    await _appNotificationManager.NotifyPlatformConnectionAwaitingEmailVerification(user.Id,
                        new List<string> {app.Id}, platform.Id, session, cancellationToken);

                    return (new PlatformConnectionStartResult(PlatformConnectionState.AwaitingEmailVerification), existingPlatformConnection, platform.IntegrationType);
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
                    await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id, new List<string> {app.Id},
                        platform.Id, session, cancellationToken);
                    var platformConnection = await HandleEmailValidationCompletion(platform.Id, platform.IntegrationType, user, app,
                        userPlatformEmailAddress, platformDataClaim, session,
                        cancellationToken);
                    return (new PlatformConnectionStartResult(PlatformConnectionState.Connected), platformConnection, platform.IntegrationType);
                case PlatformIntegrationType.Manual:
                    throw new PlatformDoesNotSupportAutomaticConnection();
                default:
                    throw new ArgumentException($"Unknown integration type {platform.IntegrationType}");

            }
        }

        public async Task<(string RedirectUrl, PlatformConnection PlatformConnection, string UserId, PlatformIntegrationType PlatformIntegrationType)> 
            CompleteConnectUserToOAuthPlatform(Guid externalPlatformId, string code,
            string stateStr,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var platform =
                await _platformManager.GetPlatformByExternalId(externalPlatformId, session, cancellationToken);

            OAuthCompleteResult completeResult = platform.IntegrationType switch
            {
                PlatformIntegrationType.FreelancerIntegration => await _freelancerAuthenticator.CompleteAuthorization(
                    code, stateStr),
                PlatformIntegrationType.AirbnbIntegration => throw new NotImplementedException(),
                PlatformIntegrationType.UpworkIntegration => throw new NotImplementedException(),
                PlatformIntegrationType.GigDataPlatformIntegration => throw new NotImplementedException(),
                PlatformIntegrationType.Manual => throw new PlatformDoesNotSupportAutomaticConnection(),
                _ => throw new ArgumentException($"Unknown integration type {platform.IntegrationType}")
            };



            var (redirectUrl, userId, platformConnection) = await HandleOAuthCompleteResult(completeResult, platform, session, cancellationToken);

            return (redirectUrl, platformConnection, userId, platform.IntegrationType);
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

        private async Task<(string RedirectUrl, string UserId, PlatformConnection PlatformConnection)> HandleOAuthCompleteResult(OAuthCompleteResult oAuthCompleteResult, Platform platform,
            IAsyncDocumentSession session, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserByExternalId(oAuthCompleteResult.UserId, session, cancellationToken);
            var app = await _appManager.GetAppFromApplicationId(oAuthCompleteResult.ApplicationId, session,
                cancellationToken);

            var existingPlatformConnection =
                user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platform.Id);
            if (existingPlatformConnection != null)
            {
                if (existingPlatformConnection.ConnectionInfo.NotificationInfos.All(ni => ni.AppId != app.Id))
                {
                    existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id,
                        oAuthCompleteResult.PlatformDataClaim ?? app.DefaultPlatformDataClaim));
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
            else
            {
                //No platform connection existed. Create a new one for the platform.
                existingPlatformConnection = new PlatformConnection(platform.Id, platform.Name, platform.ExternalId,
                    new OAuthPlatformConnectionInfo(new Token(oAuthCompleteResult.Token)),
                    platform.DataPollIntervalInSeconds);
                existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id,
                    oAuthCompleteResult.PlatformDataClaim ?? app.DefaultPlatformDataClaim));
                
                user.PlatformConnections.Add(existingPlatformConnection);
            }

            await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id, new List<string> {app.Id},
                platform.Id, session, cancellationToken);

            var redirectUrl = GetRedirectUrl(oAuthCompleteResult.RedirectUrl, platform.ExternalId);

            return (redirectUrl, user.Id, existingPlatformConnection);
        }

        private async Task<PlatformConnection> HandleEmailValidationCompletion(string platformId, PlatformIntegrationType platformIntegrationType, User user, App app,
            string userPlatformEmailAddress, PlatformDataClaim? platformDataClaim, IAsyncDocumentSession session,
            CancellationToken cancellationToken)
        {
            var existingPlatformConnection =
                user.PlatformConnections.SingleOrDefault(pc => pc.PlatformId == platformId);
            if (existingPlatformConnection != null)
            {
                //add notification info
                if (existingPlatformConnection.ConnectionInfo.NotificationInfos.All(ni => ni.AppId != app.Id))
                {
                    existingPlatformConnection.ConnectionInfo.NotificationInfos.Add(
                        new NotificationInfo(app.Id, platformDataClaim ?? app.DefaultPlatformDataClaim));
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

                return existingPlatformConnection;
            }

            var platform = await _platformManager.GetPlatform(platformId, session, cancellationToken);

            var emailConnectionInfo = new EmailPlatformConnectionInfo(userPlatformEmailAddress);
            emailConnectionInfo.NotificationInfos.Add(new NotificationInfo(app.Id, platformDataClaim ?? app.DefaultPlatformDataClaim));

            var platformConnection = new PlatformConnection(platform.Id, platform.Name, platform.ExternalId,
                emailConnectionInfo, platform.DataPollIntervalInSeconds);
            user.PlatformConnections.Add(platformConnection);
            return platformConnection;
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
