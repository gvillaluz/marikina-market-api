using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface INotificationRepository
    {
        Task<List<Notification>> GetNotificationsAsync(int enforcerId, int limit, int offset, string filter);
        Task SaveNotificationAsync(Notification notification);
    }
}