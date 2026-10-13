using MarikinaMarket.API.Application.DTOs.Reports.Request;
using MarikinaMarket.API.Application.DTOs.Reports.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IReportService
    {
        Task<ReportPreviewResponse> PreviewAsync(string? date, string type);
        Task<ReportDetailResponse> GenerateAsync(int adminId, GenerateReportRequest request);
        Task<PageResponse<ReportSummaryResponse>> GetHistoryAsync(int offset);
        Task<ReportDetailResponse> GetDetailAsync(int id);
    }
}
