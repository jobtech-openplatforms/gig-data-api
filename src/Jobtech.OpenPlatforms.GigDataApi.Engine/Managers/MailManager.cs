using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Configuration;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public interface IMailManager
    {
        Task SendConfirmEmailAddressMail(string emailAddressToConfirm, string acceptUrl, string declineUrl, CancellationToken cancellationToken = default);
    }

    public class MailManager : IMailManager
    {
        private readonly SmtpConfiguration _smtpConfiguration;
        private readonly ILogger<MailManager> _logger;

        public MailManager(IOptions<SmtpConfiguration> smtpOptions, ILogger<MailManager> logger)
        {
            _smtpConfiguration = smtpOptions.Value;
            _logger = logger;
        }

        public async Task SendConfirmEmailAddressMail(string emailAddressToConfirm, string acceptUrl, string declineUrl, CancellationToken cancellationToken = default)
        {
            if (!IsValidEmail(emailAddressToConfirm))
            {
                throw new InvalidEmailAddressException(emailAddressToConfirm);
            }

            var subject = "OpenPlatforms email ownership verification";
            var messageBody =
                $"OpenPlatforms.org would like to verify that the email address <b>{emailAddressToConfirm}</b> is controlled by you. <br/><br/>" +
                $"If you initiated the verification process from OpenPlatforms.org please click on the following link to verify ownership: <a href=\"{acceptUrl}\">Verify</a>.<br/><br/>" +
                $"Greetings from OpenPlatform.org";

            await SendMail("verify@jobtechdev.se", emailAddressToConfirm, subject, messageBody, true, cancellationToken);

        }

        private async Task SendMail(string fromAddress, string toAddress, string subject, string body,
            bool isBodyHtml = false, CancellationToken cancellationToken = default)
        {
            var mailMessage = new MailMessage { From = new MailAddress(fromAddress, "Open Platforms") };
            mailMessage.To.Add(toAddress);
            mailMessage.Subject = subject;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = isBodyHtml;

            using var aseClient = new AmazonSimpleEmailServiceV2Client(_smtpConfiguration.Username, _smtpConfiguration.Password, RegionEndpoint.EUCentral1);

            var mailBody = new Body();
            if (isBodyHtml)
            {
                mailBody.Html = new Content { Charset = "UTF-8", Data = body };
            }
            else
            {
                mailBody.Text = new Content { Charset = "UTF-8", Data = body };
            }

            var sendMailRequest = new SendEmailRequest
            {
                FromEmailAddress = fromAddress,
                Destination = new Destination { ToAddresses = new List<string> { toAddress } },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Body = mailBody,
                        Subject = new Content { Charset = "UTF-8", Data = subject }
                    }
                }
            };

            try
            {
                var sendMailResponse = await aseClient.SendEmailAsync(sendMailRequest, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Got error sending mail. Will throw");
                throw;
            }
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Normalize the domain
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                    RegexOptions.None, TimeSpan.FromMilliseconds(200));

                // Examines the domain part of the email and normalizes it.
                static string DomainMapper(Match match)
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    var idn = new IdnMapping();

                    // Pull out and process domain name (throws ArgumentException on invalid)
                    var domainName = idn.GetAscii(match.Groups[2].Value);

                    return match.Groups[1].Value + domainName;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            try
            {
                return Regex.IsMatch(email,
                    @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                    @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

    }
}
