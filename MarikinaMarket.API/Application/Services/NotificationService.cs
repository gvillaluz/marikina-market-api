using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Application.DTOs.Notification.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly INotificationRepository _notificationRepository;
        private const int PAGE_SIZE = 10;

        public NotificationService(
            IPushNotificationService pushNotificationService,
            IEmailService emailService,
            IUserRepository userRepository,
            INotificationRepository notificationRepository
            )
        {
            _pushNotificationService = pushNotificationService;
            _emailService = emailService;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
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

        public async Task SaveNotificationAsync(int ticketId, int enforcerId, string message, TicketStatus status)
        {
            if (ticketId <= 0)
                throw new ValidationException("Ticket identification is missing.");

            if (string.IsNullOrEmpty(message))
                throw new ValidationException("Message should not be empty.");

            await _notificationRepository.SaveNotificationAsync(new Notification
            {
                TicketId = ticketId,
                EnforcerId = enforcerId,
                Status = status,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            await _emailService.SendEmailAsync(toEmail, subject, body);
        }

        public async Task<bool> SendPushNotificationAsync(int userId, string title, string body, Dictionary<string, string>? data = null)
        {
            var deviceTokens = await _userRepository.GetDeviceTokensByIdAsync(userId);

            if (deviceTokens is null || deviceTokens.Count == 0)
                return false;

            var anySucceeded = false;

            foreach (var token in deviceTokens)
            {
                var success = await _pushNotificationService.SendAsync(token, title, body, data);
                if (success)
                    anySucceeded = true;
            }

            return anySucceeded;
        }
    }
}