using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IMarketSectionRepository
    {
        public Task<MarketSection?> GetById(int marketSectionId);
    }
}
