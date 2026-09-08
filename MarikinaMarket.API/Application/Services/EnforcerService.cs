using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Application.DTOs.Enforcers.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces;
using MarikinaMarket.API.Application.Interfaces.Repositories;

namespace MarikinaMarket.API.Application.Services
{
    public class EnforcerService : IEnforcerService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITicketRepository _ticketRepository;

        private const int PAGE_SIZE = 10;

        public EnforcerService(IUserRepository userRepository, ITicketRepository ticketRepository)
        {
            _userRepository = userRepository;
            _ticketRepository = ticketRepository;
        }

        public async Task<PageResponse<AdminEnforcerSummaryResponse>> GetEnforcerSummaryAsync(int offset, EnforcerSummaryFilter filters)
        {
            offset = Math.Max(offset, 0);

            var enforcers = await _userRepository.GetEnforcersAsync(offset, PAGE_SIZE, filters);

            if (enforcers is null || enforcers.Count == 0)
                return new PageResponse<AdminEnforcerSummaryResponse> { Items = [], HasMore = false };

            bool hasMore = enforcers.Count() > PAGE_SIZE;
            if (hasMore)
                enforcers.RemoveAt(enforcers.Count - 1);

            var totalEnforcers = await _userRepository.GetEnforcersCountAsync(filters);

            var enforcerTickets = await _ticketRepository.GetEnforcerTicketCountAsync(enforcers.Select(e => e.Id).ToList());

            var items = enforcers.Select(e =>
            {
                var counts = enforcerTickets.FirstOrDefault(t => t.EnforcerId == e.Id);

                return new AdminEnforcerSummaryResponse
                {
                    EnforcerId = e.Id,
                    Username = e.UserName ?? "",
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Status = e.Status,
                    ProfileUrl = "",
                    WarningViolationCount = counts?.WarningCount ?? 0,
                    TicketViolationCount = counts?.TicketCount ?? 0,
                };
            }).ToList();
            
            return new PageResponse<AdminEnforcerSummaryResponse>
            {
                Items = items,
                HasMore = hasMore,
                Total = totalEnforcers
            };
        }

        public async Task<ActivityPanelDataResponse> GetDailyTicketsAverage()
        {
            var now = DateTime.UtcNow;
            var startOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var dailyTickets = await _ticketRepository.GetTicketsWithDateAsync(startOfThisMonth);
            var topEnforcers = await _ticketRepository.GetTopEnforcersAsync(startOfThisMonth);
            
            var activeDays = dailyTickets.Count;

            var totalTickets = dailyTickets.Sum(t => t.TicketCount);
            var totalWarnings = dailyTickets.Sum(t => t.WarningCount);

            var averageTicketPerDay = activeDays == 0 ? 0 : Math.Round((double)totalTickets / activeDays, 2);
            var averageWarningPerDay = activeDays == 0 ? 0 : Math.Round((double)totalWarnings / activeDays, 2);

            return new ActivityPanelDataResponse
            {
                TotalTicketCount = totalTickets,
                TotalWarningCount = totalWarnings,
                AverageTicketsPerDay = averageTicketPerDay,
                AverageWarningsPerDay = averageWarningPerDay,
                TopEnforcers = topEnforcers
            };
        }
    }
}