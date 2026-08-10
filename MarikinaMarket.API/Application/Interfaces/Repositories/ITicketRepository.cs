using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface ITicketRepository
    {
        Task<TicketDetailResponse?> GetTicketDetailAsync(int ticketId);
        Task<int> GetNewControlNumber();
        Task<List<DuplicateOrdinance>> GetDuplicatedTickets(int vendorId, List<int> ordinanceIds);
        Task<Ticket> AddTicketAsync(Ticket ticket);
        Task<List<InspectionSummary>> GetInspectionsAsync(
            int enforcerId, 
            int offset, 
            int limit, 
            ViolationType type
        );
        Task<List<TicketSummary>> GetTicketsAsync(
            int enforcerId,
            int offset,
            int limit,
            TicketStatus status
        );
        Task SaveChangesAsync();
    }
}
