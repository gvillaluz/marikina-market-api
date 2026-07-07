using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface ITicketService
    {
        Task<TicketDetailResponse> CreateTicketAsync(CreateTicketRequest request);
        Task<FineSummaryResponse> GetOffenseCountsAndPaymentBy(List<int> ordinanceIds, int vendorId);
        Task<List<TicketDetailResponse>> GetAllByEnforcerId(int enforcerId);
    }
}
