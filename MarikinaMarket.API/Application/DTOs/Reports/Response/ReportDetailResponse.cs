namespace MarikinaMarket.API.Application.DTOs.Reports.Response
{
    public class ReportDetailResponse : ReportSummaryResponse
    {
        public required ReportStatisticsResponse Summary { get; set; }
    }

    public class ReportPreviewResponse
    {
        public required string Type { get; set; }
        public required ReportDateRangeResponse DateRange { get; set; }
        public required ReportStatisticsResponse Summary { get; set; }
    }

    public class ReportStatisticsResponse
    {
        public required ReportRecordGroupResponse Total { get; set; }
        public required ReportRecordGroupResponse Resolved { get; set; }
        public required ReportRecordGroupResponse Pending { get; set; }
        public ReportRecordGroupResponse? WarningsIssued { get; set; }
        public ReportRecordGroupResponse? TicketsIssued { get; set; }
    }

    public class ReportRecordGroupResponse
    {
        public int TotalRecords { get; set; }
        public decimal AssessedPenalties { get; set; }
    }
}
