using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Common;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IEmailValidatorManager
    {
        Task StartEmailValidation(string emailToValidate, User user, App app,
            PlatformDataClaim? platformDataClaim,
            string acceptUrl, string declineUrl, IAsyncDocumentSession session, string platformId = "None",
            CancellationToken cancellationToken = default);

        Task<EmailPrompt> CompleteEmailValidation(Guid promptId, bool result, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);

        Task ExpireAllActiveEmailValidationsForUserEmail(string email, User user,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default);
    }

    public class EmailValidatorManager : IEmailValidatorManager
    {
        private readonly IMailManager _mailManager;

        public EmailValidatorManager(IMailManager mailManager)
        {
            _mailManager = mailManager;
        }

        public async Task StartEmailValidation(string emailToValidate, User user, App app,
            PlatformDataClaim? platformDataClaim,
            string acceptUrl, string declineUrl, IAsyncDocumentSession session, string platformId = "None",
            CancellationToken cancellationToken = default)
        {
            emailToValidate = emailToValidate.ToLowerInvariant();

            var existingUserEmail = user.UserEmails.SingleOrDefault(ue => ue.Email == emailToValidate);
            if (existingUserEmail == null)
            {
                user.UserEmails.Add(new UserEmail(emailToValidate, UserEmailState.AwaitingVerification));
            }

            var existingPromptsForEmail = await session.Query<EmailPrompt>()
                .Where(ep => ep.EmailAddress == emailToValidate, true).ToListAsync(cancellationToken);
            if (existingPromptsForEmail.Any())
            {
                var unexpiredPrompt = existingPromptsForEmail.SingleOrDefault(p => !p.HasExpired());
                if (unexpiredPrompt != null)
                {
                    if (!unexpiredPrompt.PlatformIdToAppId.ContainsKey(platformId))
                    {
                        unexpiredPrompt.PlatformIdToAppId.Add(platformId, new List<string>());
                    }

                    if (unexpiredPrompt.PlatformIdToAppId[platformId].All(appId => appId != app.Id))
                    {
                        unexpiredPrompt.PlatformIdToAppId[platformId].Add(app.Id);
                    }
                }
            }

            var promptId = Guid.NewGuid();
            var expiresAt = DateTimeOffset.UtcNow.AddHours(48).ToUnixTimeSeconds();

            acceptUrl = acceptUrl.Replace("{promptId}", promptId.ToString());
            declineUrl = declineUrl.Replace("{promptId}", promptId.ToString());

            await _mailManager.SendConfirmEmailAddressMail(emailToValidate, acceptUrl, declineUrl);

            var createdPrompt = new EmailPrompt(promptId, user.Id, emailToValidate, expiresAt,
                app.Id, platformId, platformDataClaim);
            await session.StoreAsync(createdPrompt, cancellationToken);
        }

        public async Task ExpireAllActiveEmailValidationsForUserEmail(string email, User user,
            IAsyncDocumentSession session, CancellationToken cancellationToken = default)
        {
            var existingUserEmail = user.UserEmails.SingleOrDefault(ue => ue.Email == email.ToLowerInvariant());
            if (existingUserEmail == null)
            {
                throw new EmailIsNotUserEmailException(email, user.ExternalId);
            }

            var allPromptsForUserEmail = await session.Query<EmailPrompt>()
                .Where(ep => ep.UserId == user.Id && ep.EmailAddress == email)
                .Take(1024)
                .ToListAsync(cancellationToken);

            var activePromptsForUserEmail = allPromptsForUserEmail.Where(ue => !ue.HasExpired());

            foreach (var emailPrompt in activePromptsForUserEmail)
            {
                emailPrompt.MarkAsExpired();
            }
        }

        public async Task<EmailPrompt> CompleteEmailValidation(Guid promptId, bool result, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var prompt = await session.Query<EmailPrompt>()
                .SingleOrDefaultAsync(ep => ep.PromptId == promptId, cancellationToken);

            if (prompt == null)
            {
                throw new EmailPromptDoesNotExistException(promptId);
            }

            if (prompt.HasExpired())
            {
                throw new EmailPromptExpiredException(prompt.PromptId);
            }

            prompt.SetResult(result);

            return prompt;
        }
    }
}
