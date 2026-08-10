using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IOrdinanceRepository
    {
        public Task<List<OrdinanceOffenseSummary>> GetByIdsAsync(List<int> ordinanceIds, int vendorId);
        //public Task<List<OrdinancePenaltyTier>> GetPenaltyTiersAsync(List<int> ordinanceTiers);
        public Task<List<Ordinance>> GetOrdinances();
    }
}
