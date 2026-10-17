using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly AppDbContext _context;

        public NotificationRepository(AppDbContext context) => _context = context;

        public async Task<List<Notification>> GetNotificationsAsync(int enforcerId, int limit, int offset, string filter)
        {
            var query = _context.Notifications
                .Where(n => n.EnforcerId == enforcerId)
                .AsQueryable();

            if (filter == "Unread") query = query.Where(n => !n.IsRead);

            return await query
                .Include(n => n.Ticket!)
                    .ThenInclude(t => t!.MarketSection)
                .Include(n => n.Ticket!)
                    .ThenInclude(t => t!.Vendor)
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .Skip(offset)
                .Take(limit + 1)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Notification>> GetVendorNotificationsAsync(int vendorId, int limit, int offset, string filter)
        {
            return await VendorNotifications(vendorId, filter)
                .Include(notification => notification.Ticket!).ThenInclude(ticket => ticket.MarketSection)
                .Include(notification => notification.Ticket!).ThenInclude(ticket => ticket.Vendor)
                .OrderByDescending(notification => notification.CreatedAt).ThenByDescending(notification => notification.Id)
                .Skip(offset).Take(limit + 1).AsNoTracking().ToListAsync();
        }

        public async Task<int> GetVendorNotificationCountAsync(int vendorId, string filter)
            => await VendorNotifications(vendorId, filter).CountAsync();

        public async Task<Notification?> GetVendorNotificationByIdAsync(int notificationId, int vendorId)
            => await _context.Notifications.FirstOrDefaultAsync(notification => notification.Id == notificationId
                && notification.VendorId == vendorId);

        private IQueryable<Notification> VendorNotifications(int vendorId, string filter)
        {
            var query = _context.Notifications.Where(notification => notification.VendorId == vendorId);
            return filter == "Unread" ? query.Where(notification => !notification.VendorIsRead) : query;
        }

        public async Task<Notification?> GetNotificationByIdAsync(int notificationId, int enforcerId)
        {
            return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.EnforcerId == enforcerId);
        }

        public async Task SaveNotificationAsync(Notification notification)
        {
            await _context.Notifications.AddAsync(notification);
        }

        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception("A database error occured while saving the changes.");
            }
        }
    }
}
