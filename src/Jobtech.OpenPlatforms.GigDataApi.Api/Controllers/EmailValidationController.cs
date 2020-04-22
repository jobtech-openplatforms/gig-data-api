using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Documents;

namespace Jobtech.OpenPlatforms.GigDataApi.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmailValidationController : ControllerBase
    {
        private readonly IDocumentStore _documentStore;
        private readonly IEmailValidatorManager _emailValidatorManager;
        private readonly IPlatformConnectionManager _platformConnectionManager;
        private readonly IPlatformManager _platformManager;
        private readonly IAppManager _appManager;
        private readonly IUserManager _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmailValidationController(IEmailValidatorManager emailValidatorManager,
            IPlatformConnectionManager platformConnectionManager, IPlatformManager platformManager,
            IAppManager appManager, IUserManager userManager,
            IDocumentStore documentStore, IHttpContextAccessor httpContextAccessor)
        {
            _emailValidatorManager = emailValidatorManager;
            _platformConnectionManager = platformConnectionManager;
            _platformManager = platformManager;
            _appManager = appManager;
            _userManager = userManager;
            _documentStore = documentStore;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpPost("callback")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [AllowAnonymous]
        public async Task<IActionResult> PromptCallback([FromQuery(Name = "prompt_id")] string promptId,
            CancellationToken cancellationToken)
        {
            using var session = _documentStore.OpenAsyncSession();
            var prompt = await _emailValidatorManager.CompleteEmailValidation(promptId, session, cancellationToken);

            var user = await session.LoadAsync<User>(prompt.UserId, cancellationToken);

            if (prompt.Result.HasValue)
            {
                var userEmail = user.UserEmails.Single(ue => ue.Email == prompt.EmailAddress);
                userEmail.SetEmailState(prompt.Result.Value ? UserEmailState.Verified : UserEmailState.Unverified);

                if (userEmail.UserEmailState == UserEmailState.Verified)
                {
                    var appIds = prompt.PlatformIdToAppId.Values.SelectMany(v => v).Distinct();
                    var apps = await session.LoadAsync<App>(appIds, cancellationToken);

                    var appIdsToNotify = new List<string>();

                    foreach (var platformId in prompt.PlatformIdToAppId.Keys)
                    {
                        var platform = await _platformManager.GetPlatform(platformId, session, cancellationToken);
                        foreach (var appId in prompt.PlatformIdToAppId[platformId])
                        {
                            var app = apps[appId];
                            if (platformId != "None")
                            {
                                await _platformConnectionManager.ConnectUserToEmailPlatform(platform.ExternalId, user,
                                    app,
                                    prompt.EmailAddress, session, true, cancellationToken);
                            }

                            if (appIdsToNotify.All(aid => aid != appId))
                            {
                                appIdsToNotify.Add(appId);
                            }
                        }
                    }
                }
            }

            await session.SaveChangesAsync(cancellationToken);

            return Ok();
        }

        /// <summary>
        /// Initiate a email-validation flow for a given email for the user.
        /// </summary>
        /// <remarks>
        /// The flow looks like this:
        /// 
        /// 1. A mail will be sent to the given email address.
        /// 2. The email is validated when the user clicks the accept link in the mail.
        /// 3. When the email has been validated, the app will get notified via the email verification callback.</remarks>
        /// <param name="model"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost("validate-email")]
        [Produces("application/json")]
        public async Task<ActionResult<UserEmailState>> ValidateEmail(ValidateEmailModel model,
            CancellationToken cancellationToken)
        {
            var uniqueUserIdentifier = _httpContextAccessor.HttpContext.User.Identity.Name;

            using var session = _documentStore.OpenAsyncSession();
            var emailToValidate = model.Email.ToLowerInvariant();

            var user = await _userManager.GetUserByUniqueIdentifier(uniqueUserIdentifier, session, cancellationToken);
            var app = await _appManager.GetAppFromApplicationId(model.ApplicationId, session, cancellationToken);

            var existingUserEmail = user.UserEmails.SingleOrDefault(ue => ue.Email == emailToValidate);

            if (existingUserEmail != null)
            {
                if (existingUserEmail.UserEmailState == UserEmailState.Verified)
                {
                    return UserEmailState.Verified;
                }

                if (model.ResendValidationMail)
                {
                    await _emailValidatorManager.StartEmailValidation(emailToValidate, user, app, session,
                        "None", true, cancellationToken);
                    await session.SaveChangesAsync(cancellationToken);
                    return UserEmailState.AwaitingVerification;
                }

                return existingUserEmail.UserEmailState;
            }

            await _emailValidatorManager.StartEmailValidation(model.Email, user, app, session,
                cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            return UserEmailState.AwaitingVerification;
        }
    }

    public class ValidateEmailModel
    {
        /// <summary>
        /// The id of the application.
        /// </summary>
        [Required]
        public Guid ApplicationId { get; set; }

        /// <summary>
        /// The email to validate.
        /// </summary>
        [Required, EmailAddress]
        public string Email { get; set; }

        public bool ResendValidationMail { get; set; }
    }
}