namespace MarikinaMarket.API.Application.DTOs.Analytics.Response
{
    public class ViolationTrendPointResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public required string MonthName { get; set; }
        public int LowSeverityCount { get; set; }
        public int MediumSeverityCount { get; set; }
        public int HighSeverityCount { get; set; }
    }
}
