using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface ITicketService
    {
        Task<TicketDetailResponse> GetTicketDetailByIdAsync(int ticketId);
        Task<InspectionSummaryResponse> CreateTicketAsync(CreateTicketRequest request);
        Task<FineSummaryResponse> GetOffenseCountsAndPaymentBy(List<int> ordinanceIds, int vendorId);
        Task<PageResponse<InspectionSummaryResponse>> GetInspectionsByEnforcerIdAsync(int enforcerId, int offset, ViolationType type);
        Task<PageResponse<TicketSummaryResponse>> GetTicketsByEnforcerIdAsync(int enforcerId, int offset, TicketStatus status);
    }
}
