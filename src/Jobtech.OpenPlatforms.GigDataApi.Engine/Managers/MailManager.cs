using System;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jobtech.OpenPlatforms.GigDataApi.Engine.Exceptions;

namespace Jobtech.OpenPlatforms.GigDataApi.Engine.Managers
{
    public class MailManager
    {
        public async Task SendConfirmEmailAddressMail(string emailAddressToConfirm)
        {
            if (!IsValidEmail(emailAddressToConfirm))
            {
                throw new InvalidEmailAddressException(emailAddressToConfirm);
            }

            var subject = "OpenPlatforms email ownership verification";
            var messageBody =
                $"OpenPlatforms.org would like to verify that the email address <b>{emailAddressToConfirm}</b> is controlled by you. <br/><br/>" +
                $"If you initiated the verification process from OpenPlatforms.org please click on the following link to verify ownership: <a href=\"\">Verify</a>.<br/><br/>" +
                $"If you did not initiate the verification process or if you do not want to proceed with the verifications, click the following link: <a href=\"\">Don't verify</a>.<br/><br/>" +
                $"Greetings from OpenPlatform.org";

            await SendMail("verify@openplatforms.org", emailAddressToConfirm, subject, messageBody, true);

        }

        private async Task SendMail(string fromAddress, string toAddress, string subject, string body,
            bool isBodyHtml = false)
        {
            var mailMessage = new MailMessage { From = new MailAddress(fromAddress) };
            mailMessage.To.Add(toAddress);
            mailMessage.Subject = subject;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = isBodyHtml;

            using var client = new SmtpClient("mailcluster.loopia.se", 587)
            {
                Credentials = new NetworkCredential("blaj", "bloj"),
                EnableSsl = true
            };
            await client.SendMailAsync(mailMessage);
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
            catch (RegexMatchTimeoutException e)
            {
                return false;
            }
            catch (ArgumentException e)
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
