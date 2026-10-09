using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IMarketSectionRepository
    {
        public Task<MarketSection?> GetById(int marketSectionId);
        public Task<List<MarketSection>> GetAllAsync();
        public Task<int> GetActiveCountAsync();
        public Task SaveChangesAsync();
        public Task AddAsync(MarketSection section);
        public Task<bool> NameExistsAsync(string name, int? excludingId = null);
        public Task<int> GetVendorCountAsync(int id);
    }
}
