using MarikinaMarket.API.Application.DTOs.Analytics.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IAdminAnalyticsService
    {
        Task<List<ViolationTrendPointResponse>> GetViolationTrendAsync();
        Task<List<PeakViolationTimeResponse>> GetPeakViolationTimesAsync();
        Task<List<ViolationCategoryDistributionResponse>> GetViolationCategoryDistributionAsync();
        Task<InspectionRatioResponse> GetInspectionRatioAsync();
        Task<ViolationResolutionRateResponse> GetViolationResolutionRateAsync();
        Task<List<MarketSectionAggregationResponse>> GetMarketSectionAggregationAsync();
        Task<List<HotspotRankingResponse>> GetHotspotRankingAsync();
        Task<List<VendorRiskRankingResponse>> GetVendorRiskRankingAsync();
    }
}
