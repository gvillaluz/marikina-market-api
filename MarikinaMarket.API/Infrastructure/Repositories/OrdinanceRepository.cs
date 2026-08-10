using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class OrdinanceRepository : IOrdinanceRepository
    {
        private readonly AppDbContext _context;

        public OrdinanceRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<OrdinanceOffenseSummary>> GetByIdsAsync(List<int> ordinanceIds, int vendorId)
        {
            return await _context.Ordinances
                .Where(o => ordinanceIds.Contains(o.Id))
                .Select(o => new OrdinanceOffenseSummary
                {
                    OrdinanceId = o.Id,
                    OrdinanceNo = o.OrdinanceNo,
                    Code = o.Code,
                    Title = o.Title,
                    Category = o.Category,
                    PenaltyTiers = o.PenaltyTiers
                        .Select(pt => new PenaltyTierSummary
                        {
                            OrdinanceId = pt.OrdinanceId,
                            OffenseNumber = pt.OffenseNumber,
                            Severity = pt.Severity,
                            PenaltyAmount = pt.PenaltyAmount
                        }).ToList(),
                    OffenseCount = o.TicketViolations
                        .Count(tv => tv.Ticket!.VendorId == vendorId)
                })
                .ToListAsync();
        }

        public async Task<List<Ordinance>> GetOrdinances()
        {
            return await _context.Ordinances
                .Include(o => o.PenaltyTiers)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
