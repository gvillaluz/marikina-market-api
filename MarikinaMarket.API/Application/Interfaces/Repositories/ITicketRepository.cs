using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface ITicketRepository
    {
        Task<Ticket?> GetTicketByIdAsync(int ticketId);
        Task<DashboardTicketCount> GetTicketCountAsync(int enforcerId);
        Task<TicketDetailResponse?> GetTicketDetailAsync(int ticketId);
        Task<int> GetNewControlNumber();
        Task<bool> HasActiveWarningTicket(int vendorId);
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
        void SetOriginalVersion(Ticket ticket, uint version);
        Task SaveChangesAsync();
    }
}
