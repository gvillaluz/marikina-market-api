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
                .Skip(offset)
                .Take(limit + 1)
                .AsNoTracking()
                .ToListAsync();
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