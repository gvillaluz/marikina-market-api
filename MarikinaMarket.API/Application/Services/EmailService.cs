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

        public async Task SendEmailAsync(string toEmail, string subject, string body, string accentColor = "#0B2D5B")
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
                message.Body = CreateBody(body, accentColor);

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

        private static MimeEntity CreateBody(string body, string accentColor)
        {
            if (accentColor is not ("#0B2D5B" or "#4C8B5A" or "#C0392B" or "#B8792F"))
                throw new ArgumentException("Invalid email accent color.", nameof(accentColor));

            var builder = new BodyBuilder
            {
                HtmlBody = $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""><title>Marikina City Public Market</title></head>
<body style=""margin:0;padding:0;background-color:#EEF2F8;color:#5B6472;font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#EEF2F8;"">
    <tr><td align=""center"" style=""padding:24px 12px;"">
      <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""width:100%;max-width:600px;background-color:#FFFFFF;border:1px solid #D9E1EC;border-radius:12px;overflow:hidden;"">
        <tr><td style=""height:5px;background-color:{accentColor};font-size:0;line-height:0;"">&nbsp;</td></tr>
        <tr><td style=""padding:24px;background-color:#0B2D5B;"">
          <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""><tr>
            <td width=""76"" valign=""middle""><img src=""cid:marikina-city-seal"" alt=""Marikina City seal"" width=""64"" height=""64"" style=""display:block;border:0;width:64px;height:64px;""></td>
            <td valign=""middle"" style=""color:#FFFFFF;""><p style=""margin:0;font-size:18px;font-weight:bold;line-height:1.4;"">Marikina City Public Market</p><p style=""margin:4px 0 0;color:#D9E1EC;font-size:12px;line-height:1.5;"">Marikina Market Ticketing System</p></td>
          </tr></table>
        </td></tr>
        <tr><td style=""padding:28px 24px;color:#5B6472;font-size:15px;line-height:1.7;overflow-wrap:anywhere;"">{body}</td></tr>
        <tr><td style=""padding:18px 24px;border-top:1px solid #D9E1EC;background-color:#F8FAFD;""><p style=""margin:0;color:#5B6472;font-size:12px;line-height:1.6;"">This is an automated message. Please do not reply to this email. For assistance, contact the market administration office.</p></td></tr>
      </table>
    </td></tr>
  </table>
</body></html>",
                TextBody = "You have received a message from Marikina City Public Market. Please view this email in an HTML-capable email client to read the full message."
            };
            using var seal = typeof(EmailService).Assembly.GetManifestResourceStream("MarikinaMarket.CitySeal")
                ?? throw new InvalidOperationException("Email logo resource is missing.");
            using var buffer = new MemoryStream();
            seal.CopyTo(buffer);
            var logo = (MimePart)builder.LinkedResources.Add("marikina-city-seal.png", buffer.ToArray(), new ContentType("image", "png"));
            logo.ContentId = "marikina-city-seal";
            logo.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
            logo.ContentTransferEncoding = ContentEncoding.Base64;
            return builder.ToMessageBody();
        }
    }
}
