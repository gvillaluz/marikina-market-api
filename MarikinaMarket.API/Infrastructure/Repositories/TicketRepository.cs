using MarikinaMarket.API.Application.DTOs.Enforcers.Internal;
using MarikinaMarket.API.Application.DTOs.Enforcers.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class TicketRepository : ITicketRepository
    {
        private readonly AppDbContext _context;

        public TicketRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetTotalTicketCountAsync(ViolationType? type)
        {
            var query = _context.Tickets.AsQueryable();

            if (type.HasValue)
                query = query.Where(t => t.Type == type.Value);

            return await query.CountAsync();
        }

        public async Task<Ticket?> GetTicketByIdAsync(int ticketId)
        {
            return await _context.Tickets
                .Include(t => t.Vendor!.User)
                .FirstOrDefaultAsync(t => t.Id == ticketId);
        }

        public async Task<DashboardTicketCount> GetTicketCountAsync(int enforcerId)
        {
            var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-7);

            var baseQuery = _context.Tickets
                .Where(t => t.EnforcerId == enforcerId && t.IssuedAt >= sevenDaysAgo);

            var ticketCount = await baseQuery.CountAsync(t => t.Type == ViolationType.Ticket);
            var warningCount = await baseQuery.CountAsync(t => t.Type == ViolationType.Warning);

            return new DashboardTicketCount
            {
                TicketRecorded = ticketCount,
                WarningRecorded = warningCount,
                TotalRecorded = ticketCount + warningCount
            };
        }

        public async Task<TicketDetailResponse?> GetTicketDetailAsync(int ticketId)
        {
            return await _context.Tickets
                .Where(t => t.Id == ticketId)
                .Select(t => new TicketDetailResponse
                {
                    TicketId = t.Id,
                    VendorId = t.VendorId,
                    EnforcerId = t.EnforcerId,
                    ControlNumber = t.ControlNumber ?? null,
                    Type = t.Type,
                    StallNumber = t.Vendor!.StallNumber,
                    BusinessName = t.Vendor.BusinessName,
                    LastName = t.Vendor.User!.LastName,
                    FirstName = t.Vendor.User.FirstName,
                    Address = "",
                    Violations = t.TicketViolations.Select(tv => new TicketViolationSummary
                    {
                        TicketViolationId = tv.Id,
                        TicketId = t.Id,
                        OrdinanceId = tv.OrdinanceId,
                        OrdinanceNo = tv.Ordinance!.OrdinanceNo,
                        OrdinanceCode = tv.Ordinance.Code,
                        OffenseCount = tv.OffenseCount,
                        PenaltyAmount = tv.PenaltyAmount
                    }).ToList(),
                    IssuedAt = t.IssuedAt,
                    MarketSectionName = t.MarketSection!.Name,
                    Categories = t.Categories.ToList(),
                    Description = t.Description,
                    Severity = t.HighestSeverity ?? null,
                    PenaltyType = t.PenaltyType ?? null,
                    Status = t.Status,
                    DueDate = t.Type == ViolationType.Ticket
                        ? t.IssuedAt.AddDays(15) : null,
                    TotalFineAmount = t.TotalPaymentAmount ?? null,
                    TicketEvidences = t.TicketEvidences!.Select(e => e.EvidenceUrl).ToList() ?? new List<string>(),
                })
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetNewControlNumber()
        {
            var lastTicket = await _context.Tickets
                .FromSqlRaw("SELECT *, xmin FROM tickets WHERE type = 'Ticket' ORDER BY id DESC LIMIT 1 FOR UPDATE")
                .FirstOrDefaultAsync();

            if (lastTicket is null) return 1;

            return int.Parse(lastTicket!.ControlNumber!) + 1;
        }

        public async Task<bool> HasActiveWarningTicket(int vendorId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .AnyAsync(t => t.VendorId == vendorId &&
                       t.Type == ViolationType.Warning &&
                       t.Status == TicketStatus.Pending &&
                       t.IssuedAt >= DateTime.UtcNow.AddHours(-24));
        }

        public async Task<List<DuplicateOrdinance>> GetDuplicatedTickets(int vendorId, List<int> ordinanceIds)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Where(t => t.VendorId == vendorId &&
                            t.IssuedAt >= DateTime.UtcNow.AddHours(-24) &&
                            t.Status == TicketStatus.Pending &&
                            t.Type == ViolationType.Ticket)
                .SelectMany(t => t.TicketViolations)
                .Where(tv => ordinanceIds.Contains(tv.OrdinanceId))
                .Select(tv => new DuplicateOrdinance
                {
                    OrdinanceId = tv.OrdinanceId,
                    OrdinanceNo = tv.Ordinance!.OrdinanceNo,
                    OrdinanceCode = tv.Ordinance.Code
                })
                .Distinct()
                .ToListAsync();
        }

        public async Task<Ticket> AddTicketAsync(Ticket ticket)
        {
            await _context.Tickets.AddAsync(ticket);

            return ticket;
        }

        public async Task<List<InspectionSummary>> GetInspectionsAsync(
            int enforcerId, 
            int offset, 
            int limit,
            ViolationType type
            )
        {
            return await _context.Tickets
                .Where(t => t.EnforcerId == enforcerId && t.Type == type)
                .OrderByDescending(t => t.IssuedAt)
                .Skip(offset)
                .Take(limit + 1)
                .Select(t => new InspectionSummary
                {
                    Id = t.Id,
                    ControlNumber = t.ControlNumber!,
                    VendorId = t.VendorId,
                    LastName = t.Vendor!.User!.LastName,
                    FirstName = t.Vendor.User.FirstName,
                    BusinessName = t.Vendor.BusinessName,
                    MarketSectionId = t.MarketSectionId,
                    MarketSectionName = t.MarketSection!.Name,
                    StallNumber = t.Vendor.StallNumber,
                    EnforcerId = t.EnforcerId,
                    Type = t.Type,
                    Status = t.Status,
                    Severity = t.HighestSeverity,
                    Ordinances = t.TicketViolations.Select(tv => tv.Ordinance!.OrdinanceNo).ToList(),
                    IssuedAt = t.IssuedAt,
                    UpdatedAt = t.UpdatedAt
                }).ToListAsync();
        }

        public async Task<List<TicketSummary>> GetTicketsAsync(
            int enforcerId,
            int offset,
            int limit,
            TicketStatus status
            )
        {
            return await _context.Tickets
                .Where(t => t.EnforcerId == enforcerId 
                            && t.Status == status
                            && t.Type == ViolationType.Ticket)
                .OrderByDescending(t => t.IssuedAt)
                .Skip(offset)
                .Take(limit + 1)
                .Select(t => new TicketSummary
                {
                    Id = t.Id,
                    ControlNumber = t.ControlNumber!,
                    VendorId = t.VendorId,
                    BusinessName = t.Vendor!.BusinessName,
                    MarketSectionName = t.MarketSection!.Name,
                    StallNumber = t.Vendor.StallNumber,
                    EnforcerId = t.EnforcerId,
                    Status = t.Status,
                    IssuedAt = t.IssuedAt,
                    UpdatedAt = t.UpdatedAt
                }).ToListAsync();
        }

        public async Task<List<AdminInspectionSummary>> GetAdminInspectionAsync(int offset, int limit, InspectionSummaryFilters filters)
        {
            var query = _context.Tickets.AsQueryable();

            if (filters.Type.HasValue)
            {
                query = query.Where(t => t.Type == filters.Type);
            }

            if (filters.MarketSectionId.HasValue)
            {
                query = query.Where(t => t.MarketSectionId == filters.MarketSectionId);
            }

            if (!string.IsNullOrEmpty(filters.Search))
            {
                var search = filters.Search.Trim();
                query = query.Where(t =>
                    t.ControlNumber!.Contains(search) ||
                    t.Vendor!.User!.LastName.Contains(search) ||
                    t.Vendor.User.FirstName.Contains(search) ||
                    t.Vendor.BusinessName.Contains(search) ||
                    t.Vendor.StallNumber.Contains(search));
            }

            return await query
                .OrderByDescending(t => t.IssuedAt)
                .Skip(offset)
                .Take(limit + 1)
                .Select(t => new AdminInspectionSummary
                {
                    Id = t.Id,
                    EnforcerId = t.EnforcerId,
                    EnforcerLastName = t.Enforcer!.LastName,
                    EnforcerFirstName = t.Enforcer.FirstName,
                    VendorId = t.VendorId,
                    VendorLastName = t.Vendor!.User!.LastName!,
                    VendorFirstName = t.Vendor.User.FirstName!,
                    StallNumber = t.Vendor.StallNumber!,
                    BusinessName = t.Vendor.BusinessName,
                    MarketSectionId = t.MarketSectionId,
                    MarketSectionName = t.MarketSection!.Name,
                    Type = t.Type,
                    IssuedAt = t.IssuedAt
                })
                .ToListAsync();
        }

        public async Task<List<AdminTicketSummary>> GetAdminTicketAsync(int offset, int limit, TicketSummaryFilters filters)
        {
            var query = _context.Tickets
                .Where(t => t.Type == ViolationType.Ticket);

            if (filters.Status.HasValue)
                query = query.Where(t => t.Status == filters.Status.Value);

            if (filters.MarketSectionId.HasValue)
                query = query.Where(t => t.MarketSectionId == filters.MarketSectionId.Value);

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var search = filters.Search.Trim().ToLower();
                query = query.Where(t =>
                    t.ControlNumber!.Contains(search) ||
                    t.Vendor!.User!.LastName.ToLower().Contains(search) ||
                    t.Vendor.User.FirstName.ToLower().Contains(search) ||
                    t.Vendor.BusinessName.ToLower().Contains(search) ||
                    t.Vendor.StallNumber.ToLower().Contains(search));
            }

            return await query
                .OrderByDescending(t => t.IssuedAt)
                .Skip(offset)
                .Take(limit + 1)
                .Select(t => new AdminTicketSummary
                {
                    Id = t.Id,
                    ControlNumber = t.ControlNumber!,
                    EnforcerId = t.EnforcerId,
                    EnforcerFirstName = t.Enforcer!.FirstName,
                    EnforcerLastName = t.Enforcer.LastName,
                    VendorId = t.VendorId,
                    VendorFirstName = t.Vendor!.User!.FirstName,
                    VendorLastName = t.Vendor.User.LastName,
                    StallNumber = t.Vendor.StallNumber,
                    MarketSectionId = t.MarketSectionId,
                    MarketSectionName = t.MarketSection!.Name,
                    Status = t.Status ?? TicketStatus.Pending,
                    Severity = t.HighestSeverity ?? Severity.Minor,
                    PenaltyType = t.PenaltyType ?? PenaltyType.CashFine,
                    TotalPaymentAmount = t.TotalPaymentAmount ?? 0,
                    IssuedAt = t.IssuedAt,
                    Version = t.Version,
                })
                .ToListAsync();
        }

        public async Task<List<Ticket>> GetNewlyOverdueTicketsAsync()
        {
            var cutoff = DateTime.UtcNow.AddDays(-15);

            return await _context.Tickets
                .Where(t => t.Type == ViolationType.Ticket &&
                            t.IssuedAt <= cutoff &&
                            t.Status == TicketStatus.Pending)
                .Include(t => t.Vendor)
                    .ThenInclude(v => v!.User)
                .ToListAsync();
        }

        public async Task<TicketAnalyticsRaw> GetTicketAnalyticsAsync(DateTime startOfThisMonth, DateTime startOfLastMonth)
        {
            return await _context.Tickets
                .Where(t => t.Type == ViolationType.Ticket)
                .GroupBy(t => 1)
                .Select(g => new TicketAnalyticsRaw
                {
                    TotalTicketsThisMonth = g.Count(t => t.IssuedAt >= startOfThisMonth),
                    TotalTicketsLastMonth = g.Count(t => t.IssuedAt >= startOfLastMonth && t.IssuedAt < startOfThisMonth),

                    PaymentsThisMonth = g.Where(t => t.Status == TicketStatus.Pending && t.IssuedAt >= startOfThisMonth)
                                         .Sum(t => (decimal?)t.TotalPaymentAmount) ?? 0,
                    PaymentsLastMonth = g.Where(t => t.Status == TicketStatus.Pending
                                         && t.IssuedAt >= startOfLastMonth && t.IssuedAt < startOfThisMonth)
                                         .Sum(t => (decimal?)t.TotalPaymentAmount) ?? 0,

                    ResolvedViolationsThisMonth = g.Count(t => t.Status == TicketStatus.Paid || t.Status == TicketStatus.Waived && t.IssuedAt >= startOfThisMonth),
                    
                    HighSeveritiesThisMonth = g.Count(t => t.HighestSeverity == Severity.High && t.IssuedAt >= startOfThisMonth),
                    HighSeveritiesLastMonth = g.Count(t => t.HighestSeverity == Severity.High
                                         && t.IssuedAt >= startOfLastMonth && t.IssuedAt < startOfThisMonth),
                })
                .FirstOrDefaultAsync() ?? new TicketAnalyticsRaw();
        }

        public async Task<AdminTicketDetailResponse?> GetAdminTicketDetailAsync(int ticketId)
        {
            return await _context.Tickets
                .Where(t => t.Id == ticketId)
                .Select(t => new AdminTicketDetailResponse
                {
                    TicketId = t.Id,
                    VendorId = t.VendorId,
                    EnforcerId = t.EnforcerId,
                    ControlNumber = t.ControlNumber ?? null,
                    Type = t.Type,
                    StallNumber = t.Vendor!.StallNumber,
                    BusinessName = t.Vendor.BusinessName,
                    LastName = t.Vendor.User!.LastName,
                    FirstName = t.Vendor.User.FirstName,
                    Address = "",
                    Violations = t.TicketViolations.Select(tv => new TicketViolationSummary
                    {
                        TicketViolationId = tv.Id,
                        TicketId = t.Id,
                        OrdinanceId = tv.OrdinanceId,
                        OrdinanceNo = tv.Ordinance!.OrdinanceNo,
                        OrdinanceCode = tv.Ordinance.Code,
                        OffenseCount = tv.OffenseCount,
                        PenaltyAmount = tv.PenaltyAmount
                    }).ToList(),
                    IssuedAt = t.IssuedAt,
                    MarketSectionName = t.MarketSection!.Name,
                    Categories = t.Categories.ToList(),
                    Description = t.Description,
                    Severity = t.HighestSeverity ?? null,
                    PenaltyType = t.PenaltyType ?? null,
                    DueDate = t.Type == ViolationType.Ticket
                        ? t.IssuedAt.AddDays(15) : null,
                    TotalFineAmount = t.TotalPaymentAmount ?? null,
                    TicketEvidences = t.TicketEvidences!.Select(e => e.EvidenceUrl).ToList() ?? new List<string>(),
                    Status = t.Status,
                    EnforcerFirstName = t.Enforcer!.FirstName,
                    EnforcerLastName = t.Enforcer.LastName,
                    TicketReceipts = t.ReceiptUrls ?? new List<string>(),
                    Version = t.Version,
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<AdminEnforcerTicketCount>> GetEnforcerTicketCountAsync(List<int> enforcerIds)
        {
            return await _context.Tickets
                .Where(t => enforcerIds.Contains(t.EnforcerId))
                .GroupBy(t => t.EnforcerId)
                .Select(g => new AdminEnforcerTicketCount
                {
                    EnforcerId = g.Key,
                    TicketCount = g.Count(t => t.Type == ViolationType.Ticket),
                    WarningCount = g.Count(t => t.Type == ViolationType.Warning)
                }).ToListAsync();
        }

        public void SetOriginalVersion(Ticket ticket, uint version)
        {
            _context.Entry(ticket)
                .Property(t => t.Version)
                .OriginalValue = version;
        }

        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception("A database error occured while saving the changes.");
            }
        }

        private static IQueryable<Ticket> ApplyDateRange(IQueryable<Ticket> query, DateTime? from, DateTime? to)
        {
            if (from.HasValue) query = query.Where(t => t.IssuedAt >= from.Value);
            if (to.HasValue) query = query.Where(t => t.IssuedAt < to.Value);
            return query;
        }

        public async Task<List<DailyTicketCount>> GetTicketsWithDateAsync(DateTime startOfThisMonth)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Where(t => t.IssuedAt >= startOfThisMonth)
                .GroupBy(t => t.IssuedAt.Date)
                .Select(g => new DailyTicketCount
                {
                   Date = g.Key,
                   TicketCount = g.Count(t => t.Type == ViolationType.Ticket),
                   WarningCount = g.Count(t => t.Type == ViolationType.Warning)
                }).ToListAsync();
        }

        public async Task<List<TopEnforcerResponse>> GetTopEnforcersAsync(DateTime startOfThisMonth)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Where(t => t.IssuedAt >= startOfThisMonth)
                .GroupBy(t => t.EnforcerId)
                .Select(g => new TopEnforcerResponse
                {
                    EnforcerId = g.Key,
                    EnforcerName = $"{g.First().Enforcer!.LastName}, {g.First().Enforcer!.FirstName}",
                    TotalTickets = g.Count()
                })
                .OrderByDescending(g => g.TotalTickets)
                .Take(5)
                .ToListAsync();
        }
    }
}
