using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class MarketSectionRepository : IMarketSectionRepository
    {
        private readonly AppDbContext _context;

        public MarketSectionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<MarketSection?> GetById(int marketSectionId)
        {
            return await _context.MarketSections.FirstOrDefaultAsync(m => m.Id == marketSectionId);
        }
    }
}
