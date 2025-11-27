using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;   // <-- REQUIRED
using Microsoft.Extensions.Configuration;

namespace DailyLogSystem.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void SendEmail(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email cannot be null or empty.", nameof(toEmail));

            var fromEmail = _config["EmailSettings:FromEmail"]
                            ?? throw new InvalidOperationException("FromEmail is not configured.");
            var password = _config["EmailSettings:Password"]
                            ?? throw new InvalidOperationException("Password is not configured.");
            var smtpServer = _config["EmailSettings:SmtpServer"]
                            ?? throw new InvalidOperationException("SmtpServer is not configured.");
            var portValue = _config["EmailSettings:Port"];

            if (!int.TryParse(portValue, out var port))
                throw new InvalidOperationException("Invalid or missing Port configuration.");

            using var smtpClient = new SmtpClient(smtpServer)
            {
                Port = port,
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = true
            };

            using var mail = new MailMessage(fromEmail, toEmail, subject ?? "(No Subject)", body ?? "")
            {
                IsBodyHtml = true
            };

            try
            {
                smtpClient.Send(mail);
            }
            catch (SmtpException ex)
            {
                throw new InvalidOperationException("Failed to send email. Check SMTP configuration or credentials.", ex);
            }
        }

        // -----------------------------------------------------------------------------------
        // ✔ ADDED FUNCTION (Your requested version) — Does NOT modify existing logic.
        // -----------------------------------------------------------------------------------
        public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            // Reuse your existing working SendEmail()
            return Task.Run(() => SendEmail(toEmail, subject, htmlBody));
        }
    }
}
