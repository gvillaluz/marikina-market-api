using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
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
                .FromSqlRaw("SELECT * FROM tickets ORDER BY id DESC LIMIT 1 FOR UPDATE")
                .FirstOrDefaultAsync();

            if (lastTicket == null) return 1;

            return int.Parse(lastTicket.ControlNumber) + 1;
        }

        public async Task<List<DuplicateOrdinance>> GetDuplicatedTickets(int vendorId, List<int> ordinanceIds)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Where(t => t.VendorId == vendorId &&
                            t.IssuedAt >= DateTime.UtcNow.AddHours(-24) &&
                            t.Status == TicketStatus.Active)
                .SelectMany(t => t.TicketViolations)
                .Where(tv => ordinanceIds.Contains(tv.OrdinanceId))
                .Select(tv => new DuplicateOrdinance
                {
                    OrdinanceId = tv.OrdinanceId,
                    OrdinanceNo = tv.Ordinance.OrdinanceNo,
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
                    ControlNumber = t.ControlNumber,
                    VendorId = t.VendorId,
                    LastName = t.Vendor.User.LastName,
                    FirstName = t.Vendor.User.FirstName,
                    BusinessName = t.Vendor.BusinessName,
                    MarketSectionId = t.MarketSectionId,
                    MarketSectionName = t.MarketSection.Name,
                    StallNumber = t.Vendor.StallNumber,
                    EnforcerId = t.EnforcerId,
                    Type = t.Type,
                    Status = t.Status,
                    Severity = t.HighestSeverity,
                    Ordinances = t.TicketViolations.Select(tv => tv.Ordinance.OrdinanceNo).ToList(),
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
            .Where(t => t.EnforcerId == enforcerId && t.Status == status)
            .OrderByDescending(t => t.IssuedAt)
            .Skip(offset)
            .Take(limit + 1)
            .Select(t => new TicketSummary
            {
                Id = t.Id,
                ControlNumber = t.ControlNumber,
                VendorId = t.VendorId,
                BusinessName = t.Vendor.BusinessName,
                MarketSectionName = t.MarketSection.Name,
                StallNumber = t.Vendor.StallNumber,
                EnforcerId = t.EnforcerId,
                Status = t.Status,
                IssuedAt = t.IssuedAt,
                UpdatedAt = t.UpdatedAt
            }).ToListAsync();
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
    }
}
