using MarikinaMarket.API.Application.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MarikinaMarket.API.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config) => _config = config;

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var fromAddress = _config["Email:FromAddress"];
            var appPassword = _config["Email:AppPassword"];
            var smtpHost = _config["Email:SmtpHost"];
            var smtpPort = _config["Email:SmtpPort"];

            if (string.IsNullOrEmpty(fromAddress) || string.IsNullOrEmpty(appPassword))
            {
                throw new InvalidOperationException("Email configuration is missing.");
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Marikina City Public Market", fromAddress));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost!, int.Parse(smtpPort!), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(fromAddress, appPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}