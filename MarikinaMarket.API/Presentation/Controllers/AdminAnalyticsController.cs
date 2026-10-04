using MarikinaMarket.API.Application.DTOs.Analytics.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/admin/analytics")]
    [Authorize(Roles = nameof(Role.Admin))]
    public class AdminAnalyticsController : ControllerBase
    {
        private readonly IAdminAnalyticsService _service;

        public AdminAnalyticsController(IAdminAnalyticsService service)
        {
            _service = service;
        }

        [HttpGet("violation-trend")]
        public async Task<ActionResult<List<ViolationTrendPointResponse>>> GetViolationTrend()
            => Ok(await _service.GetViolationTrendAsync());

        [HttpGet("peak-violation-times")]
        public async Task<ActionResult<List<PeakViolationTimeResponse>>> GetPeakViolationTimes()
            => Ok(await _service.GetPeakViolationTimesAsync());

        [HttpGet("violation-type-distribution")]
        public async Task<ActionResult<List<ViolationCategoryDistributionResponse>>> GetViolationTypeDistribution()
            => Ok(await _service.GetViolationCategoryDistributionAsync());

        [HttpGet("inspection-ratio")]
        public async Task<ActionResult<InspectionRatioResponse>> GetInspectionRatio()
            => Ok(await _service.GetInspectionRatioAsync());

        [HttpGet("resolution-rate")]
        public async Task<ActionResult<ViolationResolutionRateResponse>> GetResolutionRate()
            => Ok(await _service.GetViolationResolutionRateAsync());

        [HttpGet("market-section-aggregation")]
        public async Task<ActionResult<List<MarketSectionAggregationResponse>>> GetMarketSectionAggregation()
            => Ok(await _service.GetMarketSectionAggregationAsync());

        [HttpGet("hotspot-ranking")]
        public async Task<ActionResult<List<HotspotRankingResponse>>> GetHotspotRanking()
            => Ok(await _service.GetHotspotRankingAsync());

        [HttpGet("vendor-risk-ranking")]
        public async Task<ActionResult<List<VendorRiskRankingResponse>>> GetVendorRiskRanking()
            => Ok(await _service.GetVendorRiskRankingAsync());
    }
}
