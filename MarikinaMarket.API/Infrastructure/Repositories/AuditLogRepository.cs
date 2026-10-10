using MarikinaMarket.API.Application.DTOs.Audits.Request;
using MarikinaMarket.API.Application.DTOs.Audits.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly AppDbContext _context;
        public AuditLogRepository(AppDbContext context) => _context = context;

        public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
        {
            var transaction = _context.Database.CurrentTransaction;
            if (transaction is not null)
                await transaction.CreateSavepointAsync("audit_log", cancellationToken);
            try
            {
                await _context.AuditLogs.AddAsync(log, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackToSavepointAsync("audit_log", cancellationToken);
                _context.Entry(log).State = EntityState.Detached;
                throw;
            }
            finally
            {
                if (transaction is not null)
                    await transaction.ReleaseSavepointAsync("audit_log", cancellationToken);
            }
        }

        public Task<List<AuditLog>> GetSummariesAsync(int offset, int limit, AuditLogFilter filters,
            DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken cancellationToken = default)
            => Filter(filters, fromUtc, toUtcExclusive)
                .OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
                .Skip(offset).Take(limit + 1).ToListAsync(cancellationToken);

        public Task<int> GetCountAsync(AuditLogFilter filters, DateTime? fromUtc, DateTime? toUtcExclusive,
            CancellationToken cancellationToken = default)
            => Filter(filters, fromUtc, toUtcExclusive).CountAsync(cancellationToken);

        public Task<AuditLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => _context.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<AuditLog>> GetRecentDashboardActivitiesAsync(int limit)
            => _context.AuditLogs
                .AsNoTracking()
                .Where(log => log.Module == Module.Tickets || log.Module == Module.Vendors ||
                    log.Module == Module.Users || log.Module == Module.Security)
                .OrderByDescending(log => log.Timestamp)
                .ThenByDescending(log => log.Id)
                .Take(limit)
                .ToListAsync();

        public async Task<AuditLogCountsResponse> GetActivityCountsAsync(AuditLogFilter filters,
            DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken cancellationToken = default)
            => await Filter(filters, fromUtc, toUtcExclusive)
                .GroupBy(x => 1)
                .Select(group => new AuditLogCountsResponse
                {
                    RecordedActivities = group.Count(),
                    SuccessfulActions = group.Count(x => x.Result == LogResult.Success),
                    SecurityEvents = group.Count(x => x.Module == Module.Security)
                })
                .SingleOrDefaultAsync(cancellationToken) ?? new AuditLogCountsResponse();

        private IQueryable<AuditLog> Filter(AuditLogFilter filters, DateTime? fromUtc, DateTime? toUtcExclusive)
        {
            var query = _context.AuditLogs.AsNoTracking();
            if (fromUtc.HasValue) query = query.Where(x => x.Timestamp >= fromUtc.Value);
            if (toUtcExclusive.HasValue) query = query.Where(x => x.Timestamp < toUtcExclusive.Value);
            if (filters.UserId.HasValue) query = query.Where(x => x.UserId == filters.UserId);
            if (filters.Role.HasValue) query = query.Where(x => x.Role == filters.Role);
            if (filters.Module.HasValue) query = query.Where(x => x.Module == filters.Module);
            if (filters.Modules.Length > 0) query = query.Where(x => filters.Modules.Contains(x.Module));
            if (filters.Result.HasValue) query = query.Where(x => x.Result == filters.Result);
            if (filters.Results.Length > 0) query = query.Where(x => filters.Results.Contains(x.Result));
            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var search = filters.Search.Trim().ToLower();
                query = query.Where(x => x.Action.ToLower().Contains(search) ||
                    x.Details.ToLower().Contains(search) || (x.TargetId != null && x.TargetId.ToLower().Contains(search)));
            }
            return query;
        }
    }
}
