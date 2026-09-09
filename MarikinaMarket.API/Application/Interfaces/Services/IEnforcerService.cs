using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Application.DTOs.Enforcers.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;

namespace MarikinaMarket.API.Application.Interfaces
{
    public interface IEnforcerService
    {
        Task<PageResponse<AdminEnforcerSummaryResponse>> GetEnforcerSummaryAsync(int offset, EnforcerSummaryFilter filters);
        Task<ActivityPanelDataResponse> GetDailyTicketsAverage();
        Task<EnforcerInfoResponse> GetProfileAsync(int enforcerId);
        Task<PerformanceSummaryResponse> GetPerformanceSummaryAsync(int enforcerId);
        Task<PageResponse<InspectionResponse>> GetInspectionHistoryAsync(int enforcerId, int offset);
    }
}