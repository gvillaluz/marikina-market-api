namespace MarikinaMarket.API.Application.Interfaces.Services
{
    using MarikinaMarket.API.Application.DTOs.MarketSection.Response;
    using MarikinaMarket.API.Application.DTOs.MarketSection.Request;
    using MarikinaMarket.API.Application.DTOs.SystemConfiguration.Response;

    public interface IMarketSectionService
    {
        public Task<MarketSectionResponse> CreateAsync(SaveMarketSectionRequest request);
        public Task<MarketSectionResponse> UpdateAsync(int id, SaveMarketSectionRequest request);
        public Task<List<MarketSectionResponse>> GetAllAsync();
        public Task<CountResponse> GetActiveCountAsync();
        public Task UpdateActiveStatusAsync(int marketSectionId, bool isActive);
    }
}
