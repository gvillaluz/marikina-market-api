namespace MarikinaMarket.API.Application.DTOs.Reports.Response
{
    public class ReportSummaryResponse
    {
        public int Id { get; set; }
        public required string Type { get; set; }
        public required ReportDateRangeResponse DateRange { get; set; }
        public DateTime DateGenerated { get; set; }
        public required ReportGeneratorResponse GeneratedBy { get; set; }
    }

    public class ReportDateRangeResponse
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
    }

    public class ReportGeneratorResponse
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
