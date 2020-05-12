using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Jobtech.OpenPlatforms.GigDataApi.Engine.IoC;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IDocumentStore _documentStore;
        private readonly IUserManager _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _auth0TenantUrl;
        private readonly string _auth0Audience;
        private readonly string _auth0MobileBankIdConnectionName;
        private readonly string _auth0DatabaseConnectionName;

        public UserController(IDocumentStore documentStore, IUserManager userManager,
            IHttpContextAccessor httpContextAccessor,
            IOptions<GigDataApiEngineServiceCollectionExtension.Auth0Configuration> auth0Options)
        {
            _documentStore = documentStore;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _auth0TenantUrl = auth0Options.Value.TenantDomain;
            _auth0Audience = auth0Options.Value.Audience;
            _auth0MobileBankIdConnectionName = auth0Options.Value.MobileBankIdConnectionName;
            _auth0DatabaseConnectionName = auth0Options.Value.DatabaseConnectionName;

        }

        [HttpGet("mobile-bank-id-authorization-url/{applicationId}")]
        [AllowAnonymous]
        [Produces("application/json")]
        public async Task<ActionResult<AuthEndpointInfoViewModel>> GetMobileBankIdAuthorizationUrl(Guid applicationId,
            [FromQuery] string redirectUrl, CancellationToken cancellationToken)
        {
            return await GetAuthorizationUrl(_auth0MobileBankIdConnectionName, applicationId, redirectUrl,
                cancellationToken);
        }

        [HttpGet("username-password-authorization-url/{applicationId}")]
        [AllowAnonymous]
        [Produces("application/json")]
        public async Task<ActionResult<AuthEndpointInfoViewModel>> GetUsernamePasswordAuthorizationUrl(
            Guid applicationId, [FromQuery] string redirectUrl, CancellationToken cancellationToken)
        {
            return await GetAuthorizationUrl(_auth0DatabaseConnectionName, applicationId, redirectUrl,
                cancellationToken);
        }

        private async Task<AuthEndpointInfoViewModel> GetAuthorizationUrl(string connectionName, Guid applicationId,
            string redirectUrl, CancellationToken cancellationToken)
        {
            using var session = _documentStore.OpenAsyncSession();
            var app = await session.Query<App>()
                .SingleOrDefaultAsync(a => a.ExternalId == applicationId, cancellationToken);

            if (app == null)
            {
                throw new AppDoesNotExistException($"App with application id {applicationId} does not exist");
            }

            var authorizeEndpointUri = $"{_auth0TenantUrl}authorize";
            var audience = _auth0Audience;
            var responseType = "id_token token";
            //var prompt = "consent";
            var scope = "openid profile name";
            var redirectUri = redirectUrl;

            var url = $"{authorizeEndpointUri}?" +
                      $"client_id={applicationId}&" +
                      $"audience={HttpUtility.UrlEncode(audience)}&" +
                      $"response_type={responseType}&" +
                      $"connection={HttpUtility.UrlEncode(connectionName)}&" +
                      $"scope={HttpUtility.UrlEncode(scope)}&" +
                      "nonce=NONCE&" +
                      $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}";

            return new AuthEndpointInfoViewModel {Url = url};
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<ActionResult<UserViewModel>> GetUser(CancellationToken cancellationToken)
        {
            var uniqueUserId = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserId, session, cancellationToken);

            return new UserViewModel(user);
        }
    }

    public class ValidatedEmailModel
    {
        [Required] public string Email { get; set; }
        [Required] public Guid UserId { get; set; }
    }

    public class AuthEndpointInfoViewModel
    {
        public string Url { get; set; }
    }

    public class UserViewModel
    {
        public UserViewModel(User user)
        {
            Id = user.ExternalId;

            UserEmails = user.UserEmails.Select(ue => new UserEmailViewModel(ue.Email, ue.UserEmailState));
        }

        public Guid Id { get; private set; }
        public IEnumerable<UserEmailViewModel> UserEmails { get; private set; }

    }

    public class UserEmailViewModel
    {
        public UserEmailViewModel(string email, UserEmailState state)
        {
            Email = email;
            State = state;
        }

        public string Email { get; private set; }
        public UserEmailState State { get; private set; }
    }
}