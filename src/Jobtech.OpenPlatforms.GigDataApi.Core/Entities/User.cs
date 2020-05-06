using System;
using System.Collections.Generic;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities.Base;
using Jobtech.OpenPlatforms.GigDataApi.Core.OAuth;

namespace Jobtech.OpenPlatforms.GigDataApi.Core.Entities
{
    public class User : BaseEntity
    {
        private User() : base()
        {
            PlatformConnections = new List<PlatformConnection>();
            UserEmails = new List<UserEmail>();
            ExternalId = Guid.NewGuid();
        }

        public User(string uniqueIdentifier) : this()
        {
            UniqueIdentifier = uniqueIdentifier;
        }


        public string Name { get; set; }
        public string UniqueIdentifier { get; private set; }
        public Guid ExternalId { get; private set; }
        public IList<PlatformConnection> PlatformConnections { get; private set; }
        public IList<UserEmail> UserEmails { get; private set; }
    }

    public class UserEmail
    {
        private UserEmail()
        {
            Created = DateTimeOffset.UtcNow;
        }

        public UserEmail(string email, UserEmailState? userEmailState = null): this()
        {
            Email = email;
            if (userEmailState.HasValue)
            {
                UserEmailState = userEmailState.Value;
                StateChanged = DateTimeOffset.UtcNow;
            }
            else
            {
                UserEmailState = UserEmailState.Unverified;
            }
            
        }

        public void SetEmailState(UserEmailState state)
        {
            StateChanged = DateTimeOffset.UtcNow;
            UserEmailState = state;
        }

        public string Email { get; private set; }
        public UserEmailState UserEmailState { get; private set; }
        public DateTimeOffset Created { get; private set; }
        public DateTimeOffset? StateChanged { get; private set; }
        public bool IsVerifiedFromApp { get; set; }
        public string VerifyingAppId { get; set; }
    }

    public class PlatformConnection
    {
        private PlatformConnection()
        {
            Created = DateTimeOffset.UtcNow;
        }

        public PlatformConnection(string platformId, string platformName, Guid externalPlatformId,
            IPlatformConnectionInfo connectionInfo, int? dataPullIntervalInSeconds = null): this()
        {
            PlatformId = platformId;
            PlatformName = platformName;
            ExternalPlatformId = externalPlatformId;
            ConnectionInfo = connectionInfo;
            DataPullIntervalInSeconds = dataPullIntervalInSeconds;
        }

        public string PlatformName { get; private set; }
        public string PlatformId { get; private set; }
        public Guid ExternalPlatformId { get; private set; }
        public IPlatformConnectionInfo ConnectionInfo { get; set; }
        public DateTimeOffset Created { get; private set; }
        public DateTimeOffset? LastDataFetchAttemptStart { get; private set; }
        public DateTimeOffset? LastDataFetchAttemptCompleted { get; private set; }
        public DateTimeOffset? LastSuccessfulDataFetch { get; private set; }
        public int? DataPullIntervalInSeconds { get; private set; }

        public void MarkAsDataFetchStarted()
        {
            LastDataFetchAttemptStart = DateTimeOffset.UtcNow;
            LastDataFetchAttemptCompleted = null;
        }

        public void MarkAsDataFetchSuccessful()
        {
            LastDataFetchAttemptCompleted = DateTimeOffset.UtcNow;
            LastSuccessfulDataFetch = LastDataFetchAttemptCompleted;
        }

        public void MarkAsDataFetchFailed()
        {
            LastDataFetchAttemptCompleted = DateTimeOffset.UtcNow;
        }
    }

    public interface IPlatformConnectionInfo
    {
        IList<NotificationInfo> NotificationInfos { get; set; }
        bool IsDeleted { get; }
    }

    public abstract class PlatformConnectionInfoBase : IPlatformConnectionInfo
    {
        protected PlatformConnectionInfoBase()
        {
            NotificationInfos = new List<NotificationInfo>();
        }

        public IList<NotificationInfo> NotificationInfos { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class OAuthPlatformConnectionInfo : PlatformConnectionInfoBase
    {
        private OAuthPlatformConnectionInfo() { }

        public OAuthPlatformConnectionInfo(Token token)
        {
            Token = token;
        }

        public Token Token { get; set; }
    }

    public class EmailPlatformConnectionInfo : PlatformConnectionInfoBase
    {
        private EmailPlatformConnectionInfo() { }

        public EmailPlatformConnectionInfo(string email)
        {
            Email = email;
        }

        public string Email { get; private set; }
    }

    public class OAuthOrEmailPlatformConnectionInfo: PlatformConnectionInfoBase
    {
        private OAuthOrEmailPlatformConnectionInfo() { }

        public OAuthOrEmailPlatformConnectionInfo(Token token)
        {
            Token = token;
        }

        public OAuthOrEmailPlatformConnectionInfo(string email)
        {
            Email = email;
        }

        public string Email { get; private set; }
        public Token Token { get; private set; }

        public bool IsOAuthAuthentication => Token != null;

        public static implicit operator OAuthOrEmailPlatformConnectionInfo(EmailPlatformConnectionInfo rhs)
        {
            return new OAuthOrEmailPlatformConnectionInfo(rhs.Email);
        }

        public static implicit operator OAuthOrEmailPlatformConnectionInfo(OAuthPlatformConnectionInfo rhs)
        {
            return new OAuthOrEmailPlatformConnectionInfo(rhs.Token);
        }
    }

    public class NotificationInfo
    {
        private NotificationInfo()
        {
            Created = DateTimeOffset.UtcNow;
        }

        public NotificationInfo(string appId, PlatformDataClaim platformDataClaim): this()
        {
            AppId = appId;
            PlatformDataClaim = platformDataClaim;
        }

        public string AppId { get; private set; }
        public PlatformDataClaim PlatformDataClaim { get; set; }
        public DateTimeOffset Created { get; private set; }
    }

    public class Token
    {
        private Token()
        {
            Created = DateTimeOffset.UtcNow;
        }

        public Token(string accessToken, string refreshToken, int expiresInSeconds): this()
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresInSeconds = expiresInSeconds;
        }

        public Token(OAuthAccessToken token) : this()
        {
            AccessToken = token.AccessToken;
            RefreshToken = token.RefreshToken;
            ExpiresInSeconds = token.ExpiresIn;
        }

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public int ExpiresInSeconds { get; private set; }
        public DateTimeOffset Created { get; private set; }

        public bool HasExpired()
        {
            return DateTimeOffset.UtcNow.Subtract(Created).TotalSeconds > ExpiresInSeconds;
        }
    }

    public enum UserEmailState
    {
        Unverified,
        AwaitingVerification,
        Verified
    }
}
