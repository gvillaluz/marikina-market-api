namespace MarikinaMarket.API.Application.DTOs.Analytics.Response
{
    public class ViolationResolutionRateResponse
    {
        public int TicketCount { get; set; }
        public int ResolvedWithinSevenDays { get; set; }
        public double ResolutionRate { get; set; }
    }
}
