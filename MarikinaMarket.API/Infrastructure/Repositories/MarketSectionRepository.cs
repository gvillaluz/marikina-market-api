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

        public async Task<int> GetActiveCountAsync()
        {
            return await _context.MarketSections.CountAsync(section => section.IsActive);
        }

        public async Task<List<MarketSection>> GetAllAsync()
        {
            return await _context.MarketSections
                .Include(section => section.VendorProfiles)
                .OrderBy(section => section.Name)
                .ToListAsync();
        }

        public async Task AddAsync(MarketSection section)
        {
            await _context.MarketSections.AddAsync(section);
        }

        public async Task<bool> NameExistsAsync(string name, int? excludingId = null)
        {
            return await _context.MarketSections.AnyAsync(s => s.Name == name
                && (!excludingId.HasValue || s.Id != excludingId.Value));
        }

        public async Task<int> GetVendorCountAsync(int id)
        {
            return await _context.MarketSections.Where(s => s.Id == id)
                .Select(s => s.VendorProfiles.Count).SingleAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
