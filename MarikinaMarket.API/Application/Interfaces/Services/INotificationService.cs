using MarikinaMarket.API.Application.DTOs.Notification.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface INotificationService
    {
        Task<bool> SendPushNotificationAsync(int userId, string title, string body, Dictionary<string, string>? data = null);
        Task<bool> SendPushNotificationToAdminsAsync(string title, string body, Dictionary<string, string>? data = null);
        Task<bool> SendPushNotificationToVendorAsync(int vendorId, string title, string body, Dictionary<string, string>? data = null);
        Task SendEmailAsync(string toEmail, string subject, string body, string accentColor = "#0B2D5B");
        Task<PageResponse<GetNotificationsResponse>> GetNotificationsByEnforcerIdAsync(int enforcerId, int offset, string filter);
        Task<PageResponse<GetVendorNotificationsResponse>> GetNotificationsByVendorUserIdAsync(int userId, int offset, string filter);
        Task SaveNotificationAsync(int ticketId, int enforcerId, int vendorId, string message, TicketStatus status);
        Task MarkAsReadNotificationAsync(int notificationId, int enforcerId);
        Task MarkVendorNotificationAsReadAsync(int notificationId, int userId);
    }
}
