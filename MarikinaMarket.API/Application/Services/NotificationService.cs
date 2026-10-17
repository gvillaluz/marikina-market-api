using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Application.DTOs.Notification.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;

namespace MarikinaMarket.API.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IVendorRepository _vendorRepository;
        private const int PAGE_SIZE = 10;
        private readonly IAuditLogService? _auditService;
        private readonly AuditLogContext? _audit;

        public NotificationService(
            IPushNotificationService pushNotificationService,
            IEmailService emailService,
            IUserRepository userRepository,
            INotificationRepository notificationRepository,
            IVendorRepository vendorRepository,
            IAuditLogService? auditService = null, AuditLogContext? audit = null
            )
        {
            _pushNotificationService = pushNotificationService;
            _emailService = emailService;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
            _vendorRepository = vendorRepository;
            _auditService = auditService;
            _audit = audit;
        }

        public async Task<PageResponse<GetNotificationsResponse>> GetNotificationsByEnforcerIdAsync(int enforcerId, int offset, string filter)
        {
            offset = Math.Max(offset, 0);

            var notifications = await _notificationRepository.GetNotificationsAsync(enforcerId, PAGE_SIZE, offset, filter);

            if (!notifications.Any()) return new PageResponse<GetNotificationsResponse> { Items = [], HasMore = false};
            
            bool hasMore = notifications.Count() > PAGE_SIZE;
            if (hasMore)
                notifications.RemoveAt(notifications.Count - 1);

            var notificationResponse = notifications.Select(n => new GetNotificationsResponse
            {
                Id = n.Id,
                EnforcerId = n.EnforcerId,
                TicketId = n.TicketId,
                ControlNumber = n.Ticket!.ControlNumber!,
                TradeName = n.Ticket.Vendor!.BusinessName,
                MarketSectionName = n.Ticket.MarketSection!.Name,
                PenaltyType = n.Ticket.PenaltyType ?? PenaltyType.CashFine,
                TotalFineAmount = n.Ticket.TotalPaymentAmount,
                DueDate = n.Ticket.IssuedAt.AddDays(5),
                Status = n.Status,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToList();

            return new PageResponse<GetNotificationsResponse>
            {
                Items = notificationResponse,
                HasMore = hasMore
            };
        }

        public async Task<PageResponse<GetVendorNotificationsResponse>> GetNotificationsByVendorUserIdAsync(int userId, int offset, string filter)
        {
            filter = ValidateVendorQuery(offset, filter);
            var vendorId = await GetVendorIdAsync(userId);
            var notifications = await _notificationRepository.GetVendorNotificationsAsync(vendorId, PAGE_SIZE, offset, filter);
            var total = await _notificationRepository.GetVendorNotificationCountAsync(vendorId, filter);
            return new PageResponse<GetVendorNotificationsResponse>
            {
                HasMore = notifications.Count > PAGE_SIZE,
                Total = total,
                Items = notifications.Take(PAGE_SIZE).Select(notification => new GetVendorNotificationsResponse
                {
                    Id = notification.Id, VendorId = notification.VendorId, EnforcerId = notification.EnforcerId,
                    TicketId = notification.TicketId, ControlNumber = notification.Ticket!.ControlNumber!,
                    TradeName = notification.Ticket.Vendor!.BusinessName,
                    MarketSectionName = notification.Ticket.MarketSection!.Name,
                    Type = notification.Ticket.Type,
                    PenaltyType = notification.Ticket.PenaltyType,
                    TotalFineAmount = notification.Ticket.TotalPaymentAmount,
                    DueDate = notification.Ticket.Type == ViolationType.Warning ? null : notification.Ticket.IssuedAt.AddDays(5),
                    Status = notification.Ticket.Type == ViolationType.Warning ? null : notification.Status,
                    Message = notification.Message, IsRead = notification.VendorIsRead, CreatedAt = notification.CreatedAt
                }).ToList()
            };
        }

        public async Task SaveNotificationAsync(int ticketId, int enforcerId, int vendorId, string message, TicketStatus status)
        {
            if (ticketId <= 0)
                throw new ValidationException("Ticket identification is missing.");

            if (enforcerId <= 0 || vendorId <= 0)
                throw new ValidationException("Valid enforcer and vendor IDs are required.");
            if (!Enum.IsDefined(status))
                throw new ValidationException("Invalid notification status.");
            if (string.IsNullOrWhiteSpace(message) || message.Length > 2000)
                throw new ValidationException("Notification message must contain between 1 and 2000 characters.");

            await _notificationRepository.SaveNotificationAsync(new Notification
            {
                TicketId = ticketId,
                EnforcerId = enforcerId,
                VendorId = vendorId,
                Status = status,
                Message = message,
                IsRead = false,
                VendorIsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, string accentColor = "#0B2D5B")
        {
            await _emailService.SendEmailAsync(toEmail, subject, body, accentColor);
        }

        public async Task<bool> SendPushNotificationAsync(int userId, string title, string body, Dictionary<string, string>? data = null)
        {
            var deviceTokens = await _userRepository.GetDeviceTokensByIdAsync(userId);

            if (deviceTokens is null || deviceTokens.Count == 0)
            {
                if (_auditService is not null)
                    await _auditService.RecordAsync(new AuditLog
                    {
                        UserId = _audit?.Entry?.UserId, Role = _audit?.Entry?.Role,
                        Action = "SendPushNotification", Module = Module.Notifications,
                        TargetId = userId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Result = LogResult.Failed, Details = $"{_audit?.PerformerDescription ?? "System"} push notification was not delivered because no devices were registered."
                    });
                return false;
            }

            var anySucceeded = false;

            foreach (var token in deviceTokens)
            {
                var success = await _pushNotificationService.SendAsync(token, title, body, data);
                if (success)
                    anySucceeded = true;
            }

            return anySucceeded;
        }

        public async Task<bool> SendPushNotificationToAdminsAsync(string title, string body, Dictionary<string, string>? data = null)
        {
            var deviceTokens = await _userRepository.GetAdminDeviceTokensAsync();
            var anySucceeded = false;

            foreach (var token in deviceTokens)
            {
                if (await _pushNotificationService.SendAsync(token, title, body, data))
                    anySucceeded = true;
            }

            return anySucceeded;
        }

        public async Task<bool> SendPushNotificationToVendorAsync(int vendorId, string title, string body, Dictionary<string, string>? data = null)
        {
            if (vendorId <= 0)
                throw new ValidationException("Vendor ID must be greater than zero.");
            var userId = await _vendorRepository.GetVendorUserIdAsync(vendorId)
                ?? throw new RecordNotFoundException("Vendor record not found.");
            return await SendPushNotificationAsync(userId, title, body, data);
        }

        public async Task MarkVendorNotificationAsReadAsync(int notificationId, int userId)
        {
            if (notificationId <= 0)
                throw new ValidationException("Notification ID must be greater than zero.");
            var vendorId = await GetVendorIdAsync(userId);
            var notification = await _notificationRepository.GetVendorNotificationByIdAsync(notificationId, vendorId)
                ?? throw new RecordNotFoundException("Notification record not found.");
            notification.VendorIsRead = true;
            await _notificationRepository.SaveChangesAsync();
        }

        private async Task<int> GetVendorIdAsync(int userId)
        {
            if (userId <= 0)
                throw new UnauthorizedAccessException("Invalid or missing identity token context.");
            return await _vendorRepository.GetVendorIdByUserIdAsync(userId)
                ?? throw new RecordNotFoundException("Vendor record not found.");
        }

        private static string ValidateVendorQuery(int offset, string filter)
        {
            if (offset < 0)
                throw new ValidationException("Offset must be zero or greater.");
            if (string.Equals(filter?.Trim(), "All", StringComparison.OrdinalIgnoreCase)) return "All";
            if (string.Equals(filter?.Trim(), "Unread", StringComparison.OrdinalIgnoreCase)) return "Unread";
            throw new ValidationException("Filter must be All or Unread.");
        }

        public async Task MarkAsReadNotificationAsync(int notificationId, int enforcerId)
        {
            var notification = await _notificationRepository.GetNotificationByIdAsync(notificationId, enforcerId);

            if (notification is null) 
                throw new RecordNotFoundException("Notification record not found.");

            notification.IsRead = true;

            await _notificationRepository.SaveChangesAsync();
        }
    }
}
