using MarikinaMarket.API.Application.DTOs.Audits.Request;
using MarikinaMarket.API.Application.DTOs.Audits.Response;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IAuditLogRepository
    {
        Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
        Task<List<AuditLog>> GetSummariesAsync(int offset, int limit, AuditLogFilter filters,
            DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken cancellationToken = default);
        Task<int> GetCountAsync(AuditLogFilter filters, DateTime? fromUtc, DateTime? toUtcExclusive,
            CancellationToken cancellationToken = default);
        Task<AuditLogCountsResponse> GetActivityCountsAsync(AuditLogFilter filters, DateTime? fromUtc,
            DateTime? toUtcExclusive, CancellationToken cancellationToken = default);
        Task<AuditLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<AuditLog>> GetRecentDashboardActivitiesAsync(int limit);
    }
}
