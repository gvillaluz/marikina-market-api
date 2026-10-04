namespace MarikinaMarket.API.Application.DTOs.Analytics.Response
{
    public class PeakViolationTimeResponse
    {
        public required string Day { get; set; }
        public int DayOfWeek { get; set; }
        public int TimeBlock { get; set; }
        public required string TimeRange { get; set; }
        public int TicketCount { get; set; }
    }
}
