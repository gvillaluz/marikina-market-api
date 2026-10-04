using System.Globalization;
using MarikinaMarket.API.Application.DTOs.Analytics.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class AdminAnalyticsService : IAdminAnalyticsService
    {
        private readonly ITicketRepository _ticketRepository;

        public AdminAnalyticsService(ITicketRepository ticketRepository)
        {
            _ticketRepository = ticketRepository;
        }

        public async Task<List<ViolationTrendPointResponse>> GetViolationTrendAsync()
        {
            var (yearStart, now) = GetCurrentYearRange();
            var buckets = await _ticketRepository.GetViolationTrendAsync(yearStart, now);
            var trend = new List<ViolationTrendPointResponse>();

            for (var month = 1; month <= 12; month++)
            {
                var monthBuckets = buckets.Where(bucket => bucket.Month == month).ToList();
                trend.Add(new ViolationTrendPointResponse
                {
                    Year = yearStart.Year,
                    Month = month,
                    MonthName = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month),
                    LowSeverityCount = monthBuckets
                        .Where(bucket => bucket.Severity == Severity.Minor)
                        .Sum(bucket => bucket.Count),
                    MediumSeverityCount = monthBuckets
                        .Where(bucket => bucket.Severity == Severity.Moderate)
                        .Sum(bucket => bucket.Count),
                    HighSeverityCount = monthBuckets
                        .Where(bucket => bucket.Severity == Severity.High)
                        .Sum(bucket => bucket.Count)
                });
            }

            return trend;
        }

        public async Task<List<PeakViolationTimeResponse>> GetPeakViolationTimesAsync()
        {
            var (yearStart, now) = GetCurrentYearRange();
            var buckets = await _ticketRepository.GetPeakViolationTimesAsync(yearStart, now);
            var results = new List<PeakViolationTimeResponse>();
            var days = new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday,
                DayOfWeek.Sunday
            };
            var timeRanges = new[]
            {
                "12AM-4AM",
                "4AM-8AM",
                "8AM-12PM",
                "12PM-4PM",
                "4PM-8PM",
                "8PM-12AM"
            };

            foreach (var day in days)
            {
                foreach (var timeRange in Enumerable.Range(0, timeRanges.Length))
                {
                    var count = buckets
                        .Where(bucket => bucket.DayOfWeek == (int)day && bucket.TimeBlock == timeRange)
                        .Sum(bucket => bucket.Count);

                    results.Add(new PeakViolationTimeResponse
                    {
                        Day = day.ToString(),
                        DayOfWeek = (int)day,
                        TimeBlock = timeRange,
                        TimeRange = timeRanges[timeRange],
                        TicketCount = count
                    });
                }
            }

            return results;
        }

        public async Task<List<ViolationCategoryDistributionResponse>> GetViolationCategoryDistributionAsync()
        {
            var (yearStart, now) = GetCurrentYearRange();
            var counts = await _ticketRepository.GetViolationCategoryDistributionAsync(yearStart, now);
            var totalTickets = await _ticketRepository.GetTotalTicketCountForPeriodAsync(
                yearStart,
                now,
                ViolationType.Ticket);
            var countsByCategory = counts.ToDictionary(item => item.Category, item => item.Count);

            return Enum.GetValues<ViolationCategory>()
                .Select(category => new
                {
                    Category = category,
                    Count = countsByCategory.TryGetValue(category, out var count) ? count : 0
                })
                .OrderByDescending(item => item.Count)
                .Select(item => new ViolationCategoryDistributionResponse
                {
                    Category = FormatCategory(item.Category),
                    TicketCount = item.Count,
                    Percentage = totalTickets == 0
                        ? 0
                        : Math.Round((double)item.Count / totalTickets * 100, 1)
                })
                .ToList();
        }

        public async Task<InspectionRatioResponse> GetInspectionRatioAsync()
        {
            var (yearStart, now) = GetCurrentYearRange();
            var counts = await _ticketRepository.GetInspectionTypeCountsAsync(yearStart, now);
            var total = counts.WarningCount + counts.TicketCount;

            return new InspectionRatioResponse
            {
                WarningCount = counts.WarningCount,
                TicketCount = counts.TicketCount,
                TotalInspectionCount = total,
                WarningPercentage = total == 0 ? 0 : Math.Round((double)counts.WarningCount / total * 100, 1),
                TicketPercentage = total == 0 ? 0 : Math.Round((double)counts.TicketCount / total * 100, 1)
            };
        }

        public async Task<ViolationResolutionRateResponse> GetViolationResolutionRateAsync()
        {
            var (yearStart, now) = GetCurrentYearRange();
            var counts = await _ticketRepository.GetViolationResolutionCountsAsync(yearStart, now);

            return new ViolationResolutionRateResponse
            {
                TicketCount = counts.TicketCount,
                ResolvedWithinSevenDays = counts.ResolvedWithinSevenDays,
                ResolutionRate = counts.TicketCount == 0
                    ? 0
                    : Math.Round((double)counts.ResolvedWithinSevenDays / counts.TicketCount * 100, 1)
            };
        }

        public async Task<List<MarketSectionAggregationResponse>> GetMarketSectionAggregationAsync()
        {
            var (yearStart, now) = GetCurrentYearRange();
            var sections = await _ticketRepository.GetMarketSectionCountsAsync(yearStart, now);

            return sections.Select(section => new MarketSectionAggregationResponse
            {
                MarketSectionId = section.MarketSectionId,
                MarketSectionName = section.MarketSectionName,
                WarningCount = section.WarningCount,
                TicketCount = section.TicketCount,
                TotalInspectionCount = section.WarningCount + section.TicketCount
            }).ToList();
        }

        public async Task<List<HotspotRankingResponse>> GetHotspotRankingAsync()
        {
            var (yearStart, now) = GetCurrentYearRange();
            var previousYearStart = yearStart.AddYears(-1);
            var previousYearEnd = previousYearStart.Add(now - yearStart);
            var sections = await _ticketRepository.GetMarketSectionTrendCountsAsync(
                yearStart,
                now,
                previousYearStart,
                previousYearEnd);

            return sections
                .OrderByDescending(section => section.CurrentWarningCount + section.CurrentTicketCount)
                .ThenBy(section => section.MarketSectionName)
                .Take(10)
                .Select((section, index) => new HotspotRankingResponse
                {
                    Rank = index + 1,
                    MarketSectionId = section.MarketSectionId,
                    MarketSectionName = section.MarketSectionName,
                    WarningCount = section.CurrentWarningCount,
                    TicketCount = section.CurrentTicketCount,
                    TotalInspectionCount = section.CurrentWarningCount + section.CurrentTicketCount,
                    PreviousPeriodCount = section.PreviousInspectionCount,
                    Trend = GetTrend(
                        section.CurrentWarningCount + section.CurrentTicketCount,
                        section.PreviousInspectionCount)
                })
                .ToList();
        }

        public async Task<List<VendorRiskRankingResponse>> GetVendorRiskRankingAsync()
        {
            var (yearStart, now) = GetCurrentYearRange();
            var vendors = await _ticketRepository.GetVendorRiskRankingAsync(yearStart, now);

            return vendors.Select((vendor, index) => new VendorRiskRankingResponse
                {
                    Rank = index + 1,
                    BusinessId = vendor.BusinessId,
                    BusinessName = vendor.BusinessName,
                    OffenseCount = vendor.OffenseCount,
                    HighestSeverity = vendor.HighestSeverity,
                    RiskScore = Math.Clamp(100 - vendor.ComplianceScore, 0, 100)
                })
                .ToList();
        }

        private static (DateTime Start, DateTime End) GetCurrentYearRange()
        {
            var now = DateTime.UtcNow;
            return (new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), now);
        }

        private static string FormatCategory(ViolationCategory category)
        {
            return category switch
            {
                ViolationCategory.WeightMeasures => "Weights & Measures",
                _ => category.ToString()
            };
        }

        private static string GetTrend(int currentCount, int previousCount)
        {
            if (currentCount > previousCount)
                return "Rising";

            if (currentCount < previousCount)
                return "Falling";

            return "Stable";
        }
    }
}
