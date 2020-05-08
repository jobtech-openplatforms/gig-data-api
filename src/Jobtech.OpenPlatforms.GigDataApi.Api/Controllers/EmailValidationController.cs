using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Api.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
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
    public class EmailValidationController : ControllerBase
    {
        private readonly IDocumentStore _documentStore;
        private readonly IEmailValidatorManager _emailValidatorManager;
        private readonly IPlatformConnectionManager _platformConnectionManager;
        private readonly IPlatformManager _platformManager;
        private readonly IAppManager _appManager;
        private readonly IAppNotificationManager _appNotificationManager;
        private readonly IUserManager _userManager;
        private readonly EmailVerificationConfiguration _emailVerificationConfiguration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmailValidationController(IEmailValidatorManager emailValidatorManager,
            IPlatformConnectionManager platformConnectionManager, IPlatformManager platformManager,
            IAppManager appManager, IAppNotificationManager appNotificationManager, IUserManager userManager,
            IOptions<EmailVerificationConfiguration> emailVerificationOptions,
            IDocumentStore documentStore, IHttpContextAccessor httpContextAccessor)
        {
            _emailValidatorManager = emailValidatorManager;
            _platformConnectionManager = platformConnectionManager;
            _platformManager = platformManager;
            _appManager = appManager;
            _appNotificationManager = appNotificationManager;
            _userManager = userManager;
            _emailVerificationConfiguration = emailVerificationOptions.Value;
            _documentStore = documentStore;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> PromptCallback([FromQuery(Name = "prompt_id")] Guid promptId, [FromQuery(Name = "accept")] bool accept,
            CancellationToken cancellationToken)
        {
            using var session = _documentStore.OpenAsyncSession();
            var prompt = await _emailValidatorManager.CompleteEmailValidation(promptId, accept, session, cancellationToken);

            var user = await session.LoadAsync<User>(prompt.UserId, cancellationToken);

            if (prompt.Result.HasValue)
            {
                var userEmail = user.UserEmails.Single(ue => ue.Email == prompt.EmailAddress);
                userEmail.SetEmailState(prompt.Result.Value ? UserEmailState.Verified : UserEmailState.Unverified);

                if (userEmail.UserEmailState == UserEmailState.Verified)
                {
                    var appIds = prompt.PlatformIdToAppId.Values.SelectMany(v => v).Distinct();
                    var apps = await session.LoadAsync<App>(appIds, cancellationToken);

                    

                    foreach (var platformId in prompt.PlatformIdToAppId.Keys)
                    {
                        Core.Entities.Platform platform;
                        if (platformId != "None")
                        {
                            platform = await _platformManager.GetPlatform(platformId, session, cancellationToken);
                        }
                        else
                        {
                            continue;
                        }

                        var appIdsToNotify = new List<string>();
                        foreach (var appId in prompt.PlatformIdToAppId[platformId])
                        {
                            var app = apps[appId];

                            await _platformConnectionManager.ConnectUserToEmailPlatform(platform.ExternalId, user,
                                app,
                                prompt.EmailAddress, _emailVerificationConfiguration.AcceptUrl,
                                _emailVerificationConfiguration.DeclineUrl, prompt.PlatformDataClaim, session, true,
                                cancellationToken);

                            if (appIdsToNotify.All(aid => aid != appId))
                            {
                                appIdsToNotify.Add(appId);
                            }
                        }

                        if (appIdsToNotify.Any())
                        {
                            await _appNotificationManager.NotifyPlatformConnectionDataUpdate(user.Id, appIdsToNotify, platform.Id,
                                session, cancellationToken);
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

            if (existingUserEmail == null)
            {
                await _emailValidatorManager.StartEmailValidation(model.Email, user, app, null,
                    _emailVerificationConfiguration.AcceptUrl, _emailVerificationConfiguration.DeclineUrl, session, "None",
                    cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
                return UserEmailState.AwaitingVerification;
            }

            if (existingUserEmail.UserEmailState == UserEmailState.Verified)
                return existingUserEmail.UserEmailState;

            await _emailValidatorManager.ExpireAllActiveEmailValidationsForUserEmail(existingUserEmail.Email,
                user, session, cancellationToken);

            await _emailValidatorManager.StartEmailValidation(emailToValidate, user, app, null,
                _emailVerificationConfiguration.AcceptUrl, _emailVerificationConfiguration.DeclineUrl, session,
                "None", cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            return existingUserEmail.UserEmailState;
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
    }
}