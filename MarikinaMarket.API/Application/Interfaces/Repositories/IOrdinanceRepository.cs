using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IOrdinanceRepository
    {
        public Task<List<OrdinanceOffenseSummary>> GetByIdsAsync(List<int> ordinanceIds, int vendorId);
        //public Task<List<OrdinancePenaltyTier>> GetPenaltyTiersAsync(List<int> ordinanceTiers);
        public Task<List<Ordinance>> GetOrdinances();
        public Task<int> GetActiveCountAsync();
        public Task<Ordinance?> GetByIdAsync(int id);
        public Task<bool> NumberExistsAsync(string ordinanceNo, int? excludingId = null);
        public Task AddAsync(Ordinance ordinance);
        public Task AddTiersAsync(IEnumerable<OrdinancePenaltyTier> tiers);
        public void RemoveTiers(IEnumerable<OrdinancePenaltyTier> tiers);
    }
}
