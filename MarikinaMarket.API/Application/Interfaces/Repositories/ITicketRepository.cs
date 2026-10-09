using MarikinaMarket.API.Application.DTOs.Enforcers.Internal;
using MarikinaMarket.API.Application.DTOs.Enforcers.Response;
using MarikinaMarket.API.Application.DTOs.Analytics.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface ITicketRepository
    {
        Task<int> GetTotalTicketCountAsync(ViolationType? type);
        Task<int> GetTotalTicketCountForPeriodAsync(DateTime start, DateTime end, ViolationType type);
        Task<Ticket?> GetTicketByIdAsync(int ticketId);
        Task<List<VendorComplianceTicket>> GetVendorComplianceTicketsAsync(
            IReadOnlyCollection<int> vendorIds, DateTime windowStart, DateTime calculatedAt);
        Task<DashboardTicketCount> GetTicketCountAsync(int enforcerId);
        Task<List<DailyTicketCount>> GetTicketsWithDateAsync(DateTime startOfThisMonth);
        Task<TicketDetailResponse?> GetTicketDetailAsync(int ticketId);
        Task<int> GetNewControlNumber();
        Task<List<WarningOrdinance>> GetWarningOrdinancesForVendor(int vendorId, List<int> ordinanceIds);
        Task<List<DuplicateOrdinance>> GetDuplicatedTickets(int vendorId, List<int> ordinanceIds);
        Task<Ticket> AddTicketAsync(Ticket ticket);
        Task<List<InspectionSummary>> GetInspectionsAsync(
            int enforcerId, 
            int offset, 
            int limit, 
            ViolationType type,
            string search
        );
        Task<List<TicketSummary>> GetTicketsAsync(
            int enforcerId,
            int offset,
            int limit,
            TicketStatus status,
            string search
        );
        Task<List<AdminInspectionSummary>> GetAdminInspectionAsync(int offset, int limit, InspectionSummaryFilters filters);
        Task<List<AdminTicketSummary>> GetAdminTicketAsync(int offset, int limit, TicketSummaryFilters filters);
        Task<TicketAnalyticsRaw> GetTicketAnalyticsAsync(DateTime startOfThisMonth, DateTime startOfLastMonth);
        Task<AdminTicketDetailResponse?> GetAdminTicketDetailAsync(int ticketId);
        Task<List<Ticket>> GetNewlyOverdueTicketsAsync();
        Task<List<AdminEnforcerTicketCount>> GetEnforcerTicketCountAsync(List<int> enforcerIds);
        Task<List<TopEnforcerResponse>> GetTopEnforcersAsync(DateTime startOfThisMonth);
        Task<DateTime> GetLastEnforcerInspectionAsync(int enforcerId);
        Task<PerformanceSummaryResponse?> GetEnforcerPerformanceSummaryAsync(int enforcerId, DateTime yearStart);
        Task<List<InspectionSummary>> GetEnforcerInspectionHistory(int enforcerId, int offset, int limit);
        Task<int> GetTotalIssuedTicketsByEnforcerIdAsync(int enforcerId);
        Task<List<VendorInspectionHistoryItem>> GetVendorInspectionHistoryAsync(
            int vendorId,
            int offset,
            int limit,
            VendorInspectionHistoryFilters filters);
        Task<int> GetVendorInspectionHistoryCountAsync(int vendorId, VendorInspectionHistoryFilters filters);
        Task<List<ViolationTrendBucket>> GetViolationTrendAsync(DateTime start, DateTime end);
        Task<List<PeakViolationTimeBucket>> GetPeakViolationTimesAsync(DateTime start, DateTime end);
        Task<List<ViolationCategoryBucket>> GetViolationCategoryDistributionAsync(DateTime start, DateTime end);
        Task<InspectionTypeCounts> GetInspectionTypeCountsAsync(DateTime start, DateTime end);
        Task<ResolutionCounts> GetViolationResolutionCountsAsync(DateTime start, DateTime end);
        Task<List<MarketSectionCount>> GetMarketSectionCountsAsync(DateTime start, DateTime end);
        Task<List<MarketSectionTrendCount>> GetMarketSectionTrendCountsAsync(
            DateTime currentStart,
            DateTime currentEnd,
            DateTime previousStart,
            DateTime previousEnd);
        Task<List<VendorRiskRankingItem>> GetVendorRiskRankingAsync(DateTime start, DateTime end);
        Task<List<AdminCommunityServiceLogSummary>> GetCommunityServiceLogsAsync(
            int offset,
            int limit,
            TicketStatus? status);
        Task<int> GetCommunityServiceLogCountAsync(TicketStatus? status);
        void SetOriginalVersion(Ticket ticket, uint version);
        Task SaveChangesAsync();
    }
}
