using MarikinaMarket.API.Application.DTOs.Notification.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface INotificationService
    {
        Task<bool> SendPushNotificationAsync(int userId, string title, string body, Dictionary<string, string>? data = null);
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task<PageResponse<GetNotificationsResponse>> GetNotificationsByEnforcerIdAsync(int enforcerId, int offset, string filter);
        Task SaveNotificationAsync(int ticketId, int enforcerId, string message, TicketStatus status);
        Task MarkAsReadNotificationAsync(int notificationId, int enforcerId);
    }
}