using FirebaseAdmin.Messaging;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly IAuditLogService _auditService;
        private readonly AuditLogContext _audit;
        public PushNotificationService(IAuditLogService auditService, AuditLogContext audit)
        {
            _auditService = auditService;
            _audit = audit;
        }

        public async Task<bool> SendAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null)
        {
            var result = LogResult.Failed;
            try
            {
                var message = new Message()
                {
                    Token = deviceToken,
                    Notification = new Notification{ Title = title, Body = body },
                    Data = data  
                };

                await FirebaseMessaging.DefaultInstance.SendAsync(message);
                result = LogResult.Success;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send notification: " + ex);
                return false;
            }
            finally
            {
                await _auditService.RecordAsync(new MarikinaMarket.API.Domain.Entities.AuditLog
                {
                    UserId = _audit.Entry?.UserId, Role = _audit.Entry?.Role,
                    Action = "SendPushNotification", Module = Module.Notifications, TargetId = _audit.Entry?.TargetId,
                    Result = result, Details = $"{_audit.PerformerDescription} push notification delivery " +
                        (result == LogResult.Success ? "completed." : "failed.")
                });
            }
        }
    }
}
