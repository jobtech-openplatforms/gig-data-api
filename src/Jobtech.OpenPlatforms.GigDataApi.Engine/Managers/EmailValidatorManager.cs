using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Core.Entities;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IEmailValidatorManager
    {
        Task StartEmailValidation(string emailToValidate, User user, App app, IAsyncDocumentSession session,
            string platformId = "None", bool shouldResendPrompt = false, CancellationToken cancellationToken = default);

        Task<EmailPrompt> CompleteEmailValidation(string promptId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default);
    }

    public class EmailValidatorManager : IEmailValidatorManager
    {
        private readonly ApproveApiHttpClient _approveApiHttpClient;

        public EmailValidatorManager(ApproveApiHttpClient approveApiHttpClient)
        {
            _approveApiHttpClient = approveApiHttpClient;
        }

        public async Task StartEmailValidation(string emailToValidate, User user, App app,
            IAsyncDocumentSession session, string platformId = "None", bool shouldResendPrompt = false,
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
                    if (shouldResendPrompt)
                    {
                        unexpiredPrompt.MarkAsExpired();
                    }
                    else
                    {
                        if (!unexpiredPrompt.PlatformIdToAppId.ContainsKey(platformId))
                        {
                            unexpiredPrompt.PlatformIdToAppId.Add(platformId, new List<string>());
                        }

                        if (unexpiredPrompt.PlatformIdToAppId[platformId].All(appId => appId != app.Id))
                        {
                            unexpiredPrompt.PlatformIdToAppId[platformId].Add(app.Id);
                        }

                        return;
                    }
                }
            }

            var (promptId, expiresIn) = await _approveApiHttpClient.SendEmailValidationPrompt(emailToValidate);

            var createdPrompt = new EmailPrompt(promptId, user.Id, emailToValidate, expiresIn,
                app.Id, platformId);
            await session.StoreAsync(createdPrompt, cancellationToken);
        }

        public async Task<EmailPrompt> CompleteEmailValidation(string promptId, IAsyncDocumentSession session,
            CancellationToken cancellationToken = default)
        {
            var prompt = await session.Query<EmailPrompt>()
                .SingleOrDefaultAsync(ep => ep.PromptId == promptId, cancellationToken);

            if (prompt == null)
            {
                throw new EmailPromptDoesNotExistException(promptId);
            }

            var promptAnswer = await _approveApiHttpClient.GetPromptAnswer(promptId);

            if (promptAnswer.HasValue)
            {
                prompt.SetResult(promptAnswer.Value);
            }

            return prompt;
        }
    }
}
