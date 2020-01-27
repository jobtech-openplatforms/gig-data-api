namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IDocumentStore _documentStore;
        private readonly IUserManager _userManager;
        private readonly IAppManager _appManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _auth0TenantUrl;
        private readonly string _auth0CVDataAudience;
        private readonly string _auth0MobileBankIdConnectionName;
        private readonly string _auth0DatabaseConnectionName;

        public UserController(IDocumentStore documentStore, IUserManager userManager, IAppManager appManager,
            IHttpContextAccessor httpContextAccessor,
            IOptions<CVDataEngineServiceCollectionExtension.Auth0Configuration> auth0Options)
        {
            _documentStore = documentStore;
            _userManager = userManager;
            _appManager = appManager;
            _httpContextAccessor = httpContextAccessor;
            _auth0TenantUrl = auth0Options.Value.TenantDomain;
            _auth0CVDataAudience = auth0Options.Value.CVDataAudience;
            _auth0MobileBankIdConnectionName = auth0Options.Value.MobileBankIdConnectionName;
            _auth0DatabaseConnectionName = auth0Options.Value.DatabaseConnectionName;

        }

        [HttpGet("mobile-bank-id-authorization-url/{applicationId}")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthEndpointInfoViewModel>> GetMobileBankIdAuthorizationUrl(string applicationId, [FromQuery] string redirectUrl)
        {
            return await GetAuthorizationUrl(_auth0MobileBankIdConnectionName, applicationId, redirectUrl);
        }

        [HttpGet("username-password-authorization-url/{applicationId}")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthEndpointInfoViewModel>> GetUsernamePasswordAuthorizationUrl(string applicationId, [FromQuery] string redirectUrl)
        {
            return await GetAuthorizationUrl(_auth0DatabaseConnectionName, applicationId, redirectUrl);
        }

        private async Task<AuthEndpointInfoViewModel> GetAuthorizationUrl(string connectionName, string applicationId, string redirectUrl)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var app = await session.Query<App>().SingleOrDefaultAsync(a => a.ApplicationId == applicationId);

                if (app == null)
                {
                    throw new AppDoesNotExistException($"App with application id {applicationId} does not exist");
                }
            }

            var authorizeEndpointUri = $"{_auth0TenantUrl}authorize";
            var audience = _auth0CVDataAudience;
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

            return new AuthEndpointInfoViewModel { Url = url };
        }

        [HttpPost("add-validated-email-address")]
        [AllowAnonymous]
        public async Task AddValidatedEmailAddress([FromHeader(Name = "app_secret")] string appSecret, [FromBody] ValidatedEmailModel model)
        {
            using (var session = _documentStore.OpenAsyncSession())
            {
                var user = await _userManager.GetUserByExternalId(model.UserId, session);
                var app = await _appManager.GetAppFromSecretKey(appSecret, session);

                var existingUserEmail =
                    user.UserEmails.SingleOrDefault(ue => ue.Email == model.Email.ToLowerInvariant());

                if (existingUserEmail != null)
                {
                    if (existingUserEmail.UserEmailState != UserEmailState.Verified)
                    {
                        existingUserEmail.SetEmailState(UserEmailState.Verified);
                        existingUserEmail.IsVerifiedFromApp = true;
                        existingUserEmail.VerifyingAppId = app.Id;
                    }
                }
                else
                {
                    var newUserEmail = new UserEmail(model.Email.ToLowerInvariant(), UserEmailState.Verified)
                    {
                        IsVerifiedFromApp = true, VerifyingAppId = app.Id
                    };
                    user.UserEmails.Add(newUserEmail);
                }

                await session.SaveChangesAsync();
            }
        }

        [HttpGet]
        public async Task<ActionResult<UserViewModel>> GetUser()
        {
            var uniqueUserId = _httpContextAccessor.HttpContext.User.Identity.Name;

            using (var session = _documentStore.OpenAsyncSession())
            {
                var user = await _userManager.GetOrCreateUserIfNotExists(uniqueUserId, session);
                return new UserViewModel(user);
            }
        }
    }

    public class ValidatedEmailModel
    {
        [Required]
        public string Email { get; set; }
        [Required]
        public Guid UserId { get; set; }
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
        }

        public Guid Id { get; }

    }
}