using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface INotificationRepository
    {
        Task<List<Notification>> GetNotificationsAsync(int enforcerId, int limit, int offset, string filter);
        Task<List<Notification>> GetVendorNotificationsAsync(int vendorId, int limit, int offset, string filter);
        Task<int> GetVendorNotificationCountAsync(int vendorId, string filter);
        Task<Notification?> GetVendorNotificationByIdAsync(int notificationId, int vendorId);
        Task SaveNotificationAsync(Notification notification);
        Task<Notification?> GetNotificationByIdAsync(int notificationId, int enforcerId);
        Task SaveChangesAsync();
    }
}
