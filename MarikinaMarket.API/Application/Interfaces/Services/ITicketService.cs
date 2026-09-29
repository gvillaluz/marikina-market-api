using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface ITicketService
    {
        Task<MobileDashboardSummaryResponse> GetMobileTicketCountAsync(int enforcerId);
        Task<TicketDetailResponse> GetTicketDetailByIdAsync(int ticketId);
        Task<TicketSettlementResponse> SubmitTicketSettlementAsync(int ticketId, int enforcerId, SubmitTicketSettlementRequest request);
        Task<InspectionSummaryResponse> CreateTicketAsync(CreateTicketRequest request);
        Task<FineSummaryResponse> GetOffenseCountsAndPaymentBy(List<int> ordinanceIds, int vendorId);
        Task<PageResponse<InspectionSummaryResponse>> GetInspectionsByEnforcerIdAsync(int enforcerId, int offset, ViolationType type, string search);
        Task<PageResponse<TicketSummaryResponse>> GetTicketsByEnforcerIdAsync(int enforcerId, int offset, TicketStatus status, string search);
        Task<UpdateStatusResponse> UpdateTicketStatusAsync(int ticketId, UpdateStatusRequest request);
        Task<PageResponse<AdminInspectionSummaryResponse>> GetAdminInspectionAsync(int offset, InspectionSummaryFilters filters);
        Task<PageResponse<AdminTicketSummary>> GetAdminTicketAsync(int offset, TicketSummaryFilters filters);
        Task<TicketAnalyticsResponse> GetTicketAnalyticsAsync();
        Task<AdminTicketDetailResponse> GetAdminTicketDetailAsync(int ticketId);
        Task<int> CheckAndNotifyOverdueTicketsAsync();
    }
}
