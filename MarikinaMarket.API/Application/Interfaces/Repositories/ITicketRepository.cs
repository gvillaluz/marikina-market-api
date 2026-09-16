using MarikinaMarket.API.Application.DTOs.Enforcers.Internal;
using MarikinaMarket.API.Application.DTOs.Enforcers.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface ITicketRepository
    {
        Task<int> GetTotalTicketCountAsync(ViolationType? type);
        Task<Ticket?> GetTicketByIdAsync(int ticketId);
        Task<DashboardTicketCount> GetTicketCountAsync(int enforcerId);
        Task<List<DailyTicketCount>> GetTicketsWithDateAsync(DateTime startOfThisMonth);
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
        Task<List<AdminInspectionSummary>> GetAdminInspectionAsync(int offset, int limit, InspectionSummaryFilters filters);
        Task<List<AdminTicketSummary>> GetAdminTicketAsync(int offset, int limit, TicketSummaryFilters filters);
        Task<TicketAnalyticsRaw> GetTicketAnalyticsAsync(DateTime startOfThisMonth, DateTime startOfLastMonth);
        Task<AdminTicketDetailResponse?> GetAdminTicketDetailAsync(int ticketId);
        Task<List<Ticket>> GetNewlyOverdueTicketsAsync();
        Task<List<AdminEnforcerTicketCount>> GetEnforcerTicketCountAsync(List<int> enforcerIds);
        Task<List<TopEnforcerResponse>> GetTopEnforcersAsync(DateTime startOfThisMonth);
        Task<DateTime> GetLastEnforcerInspectionAsync(int enforcerId);
        Task<PerformanceSummaryResponse?> GetEnforcerPerformanceSummaryAsync(int enforcerId, DateTime yearStart);
        Task<List<InspectionSummary>> GetEnforcerInspectionHistory(int enforcerId, int offset, int limit);
        Task<int> GetTotalIssuedTicketsByEnforcerIdAsync(int enforcerId);
        void SetOriginalVersion(Ticket ticket, uint version);
        Task SaveChangesAsync();
    }
}
