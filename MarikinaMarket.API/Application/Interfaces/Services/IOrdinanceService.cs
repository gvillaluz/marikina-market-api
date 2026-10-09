using MarikinaMarket.API.Application.DTOs.Ordinance.Response;
using MarikinaMarket.API.Application.DTOs.SystemConfiguration.Response;
using MarikinaMarket.API.Application.DTOs.Ordinance.Request;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IOrdinanceService
    {
        public Task<List<OrdinanceSummaryResponse>> GetOrdinances();
        public Task<OrdinanceDetailResponse> GetByIdAsync(int id);
        public Task<OrdinanceSummaryResponse> CreateAsync(SaveOrdinanceRequest request);
        public Task<OrdinanceSummaryResponse> UpdateAsync(int id, SaveOrdinanceRequest request);
        public Task<CountResponse> GetActiveCountAsync();
    }
}
