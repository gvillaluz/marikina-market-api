using MarikinaMarket.API.Application.DTOs.Analytics.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IDashboardService
    {
        Task<DashboardSummaryResponse> GetSummaryAsync();
    }
}
