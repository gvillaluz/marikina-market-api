using MarikinaMarket.API.Application.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly IAuditLogService _auditService;
        private readonly AuditLogContext _audit;
        public EmailService(IConfiguration config, IAuditLogService auditService, AuditLogContext audit)
        {
            _config = config;
            _auditService = auditService;
            _audit = audit;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var result = LogResult.Failed;
            try
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
                var builder = new BodyBuilder
                {
                    HtmlBody = body,
                    TextBody = "Please view this email in an HTML-capable email client to see your verification code."
                };
                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost!, int.Parse(smtpPort!), SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(fromAddress, appPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                result = LogResult.Success;
            }
            finally
            {
                await _auditService.RecordAsync(new AuditLog
                {
                    UserId = _audit.Entry?.UserId, Role = _audit.Entry?.Role,
                    Action = "SendEmail", Module = Module.Notifications, TargetId = _audit.Entry?.TargetId,
                    Result = result, Details = $"{_audit.PerformerDescription} email delivery " +
                        (result == LogResult.Success ? "completed." : "failed.")
                });
            }
        }
    }
}
