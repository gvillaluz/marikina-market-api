using MarikinaMarket.API.Application.DTOs.Audits.Request;
using MarikinaMarket.API.Application.DTOs.Audits.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IAuditLogService
    {
        Task<PageResponse<AuditLogSummaryResponse>> GetSummariesAsync(int offset, AuditLogFilter filters,
            CancellationToken cancellationToken = default);
        Task<AuditLogDetailResponse> GetDetailsAsync(int id, CancellationToken cancellationToken = default);
        Task<AuditLogCountsResponse> GetActivityCountsAsync(AuditLogFilter filters,
            CancellationToken cancellationToken = default);
        Task RecordAsync(AuditLog log);
        Task StageCurrentAsync();
        Task AddInTransactionAsync(AuditLog log);
    }
}
