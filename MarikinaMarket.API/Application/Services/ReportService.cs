using MarikinaMarket.API.Application.DTOs.Reports.Request;
using MarikinaMarket.API.Application.DTOs.Reports.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace MarikinaMarket.API.Application.Services
{
    public class ReportService : IReportService
    {
        private const int PAGE_SIZE = 10;
        private readonly IReportRepository _reports;
        private readonly ITicketRepository _tickets;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;

        public ReportService(IReportRepository reports, ITicketRepository tickets,
            IUserRepository users, IUnitOfWork unitOfWork)
        {
            _reports = reports;
            _tickets = tickets;
            _users = users;
            _unitOfWork = unitOfWork;
        }

        public async Task<ReportPreviewResponse> PreviewAsync(string? date, string type)
        {
            var (from, to, startUtc, endUtc) = ValidateRange(date, date, type);
            var snapshot = await BuildSnapshotAsync(from, to, startUtc, endUtc, type);
            return new ReportPreviewResponse
            {
                Type = type,
                DateRange = new ReportDateRangeResponse { From = from, To = to },
                Summary = Statistics(snapshot)
            };
        }

        public async Task<ReportDetailResponse> GenerateAsync(int adminId, GenerateReportRequest request)
        {
            Validator.ValidateObject(request, new ValidationContext(request), true);
            var (from, to, startUtc, endUtc) = ValidateRange(request.From, request.To, request.Type);
            if (adminId <= 0) throw new UnauthorizedAccessException("A valid administrator claim is required.");
            var names = await _users.GetNamesByIdsAsync([adminId]);
            if (!names.TryGetValue(adminId, out var admin))
                throw new UnauthorizedAccessException("Administrator account not found.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var report = await BuildSnapshotAsync(from, to, startUtc, endUtc, request.Type!);
                report.DateGenerated = DateTime.UtcNow;
                report.GeneratedById = adminId;
                report.GeneratedByName = $"{admin.FirstName} {admin.LastName}";
                await _reports.AddAsync(report);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return Detail(report);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<PageResponse<ReportSummaryResponse>> GetHistoryAsync(int offset)
        {
            if (offset < 0) throw new ValidationException("Offset must be zero or greater.");
            var reports = await _reports.GetHistoryAsync(offset, PAGE_SIZE);
            var total = await _reports.GetCountAsync();
            var hasMore = reports.Count > PAGE_SIZE;
            if (hasMore) reports.RemoveAt(reports.Count - 1);
            return new PageResponse<ReportSummaryResponse>
            {
                Items = reports.Select(Summary).ToList(), HasMore = hasMore, Total = total
            };
        }

        public async Task<ReportDetailResponse> GetDetailAsync(int id)
        {
            if (id <= 0) throw new ValidationException("Report ID must be greater than zero.");
            var report = await _reports.GetByIdAsync(id)
                ?? throw new RecordNotFoundException("Report not found.");
            return Detail(report);
        }

        private async Task<Report> BuildSnapshotAsync(DateOnly from, DateOnly to,
            DateTime startUtc, DateTime endUtc, string type)
        {
            ViolationType? ticketType = type switch
            {
                "Warning" => ViolationType.Warning,
                "Ticket" => ViolationType.Ticket,
                _ => null
            };
            var rows = await _tickets.GetReportSummaryAsync(startUtc, endUtc, ticketType);
            return new Report
            {
                Type = type, From = from, To = to, GeneratedByName = string.Empty,
                WarningRecords = rows.Where(x => x.Type == ViolationType.Warning).Sum(x => x.TotalRecords),
                ResolvedTicketRecords = rows.Where(x => x.Type == ViolationType.Ticket && x.IsResolved).Sum(x => x.TotalRecords),
                PendingTicketRecords = rows.Where(x => x.Type == ViolationType.Ticket && !x.IsResolved).Sum(x => x.TotalRecords),
                ResolvedTicketPenalties = rows.Where(x => x.Type == ViolationType.Ticket && x.IsResolved).Sum(x => x.AssessedPenalties),
                PendingTicketPenalties = rows.Where(x => x.Type == ViolationType.Ticket && !x.IsResolved).Sum(x => x.AssessedPenalties)
            };
        }

        private static (DateOnly From, DateOnly To, DateTime StartUtc, DateTime EndUtc) ValidateRange(
            string? fromValue, string? toValue, string? type)
        {
            if (type is not ("Warning" or "Ticket" or "Warning & Ticket"))
                throw new ValidationException("Type must be Warning, Ticket, or Warning & Ticket.");
            if (!DateOnly.TryParseExact(fromValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
                !DateOnly.TryParseExact(toValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
                throw new ValidationException("Dates are required and must use YYYY-MM-DD.");
            if (from > to) throw new ValidationException("From must be on or before to.");
            if (from == DateOnly.MinValue || to == DateOnly.MaxValue)
                throw new ValidationException("Dates are outside the supported report range.");
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
            return (from, to, startUtc, endUtc);
        }

        private static ReportSummaryResponse Summary(Report report) => new()
        {
            Id = report.Id, Type = report.Type,
            DateRange = new ReportDateRangeResponse { From = report.From, To = report.To },
            DateGenerated = report.DateGenerated,
            GeneratedBy = new ReportGeneratorResponse { Id = report.GeneratedById, Name = report.GeneratedByName }
        };

        private static ReportDetailResponse Detail(Report report)
        {
            var summary = Summary(report);
            return new ReportDetailResponse
            {
                Id = summary.Id, Type = summary.Type, DateRange = summary.DateRange,
                DateGenerated = summary.DateGenerated, GeneratedBy = summary.GeneratedBy,
                Summary = Statistics(report)
            };
        }

        private static ReportStatisticsResponse Statistics(Report report)
        {
            var warnings = Breakdown(report.WarningRecords, 0, 0m, 0m);
            var tickets = Breakdown(report.ResolvedTicketRecords, report.PendingTicketRecords,
                report.ResolvedTicketPenalties, report.PendingTicketPenalties);
            var combined = Breakdown(report.WarningRecords + report.ResolvedTicketRecords,
                report.PendingTicketRecords, report.ResolvedTicketPenalties, report.PendingTicketPenalties);
            return new ReportStatisticsResponse
            {
                Total = combined.Total, Resolved = combined.Resolved, Pending = combined.Pending,
                WarningsIssued = report.Type == "Ticket" ? null : warnings,
                TicketsIssued = report.Type == "Warning" ? null : tickets
            };
        }

        private static ReportStatusBreakdownResponse Breakdown(int resolved, int pending,
            decimal resolvedPenalties, decimal pendingPenalties) => new()
        {
            Total = new ReportRecordGroupResponse { TotalRecords = resolved + pending, AssessedPenalties = resolvedPenalties + pendingPenalties },
            Resolved = new ReportRecordGroupResponse { TotalRecords = resolved, AssessedPenalties = resolvedPenalties },
            Pending = new ReportRecordGroupResponse { TotalRecords = pending, AssessedPenalties = pendingPenalties }
        };
    }
}
