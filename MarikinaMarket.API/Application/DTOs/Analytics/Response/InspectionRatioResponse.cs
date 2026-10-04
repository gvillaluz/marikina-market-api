namespace MarikinaMarket.API.Application.DTOs.Analytics.Response
{
    public class InspectionRatioResponse
    {
        public int WarningCount { get; set; }
        public int TicketCount { get; set; }
        public int TotalInspectionCount { get; set; }
        public double WarningPercentage { get; set; }
        public double TicketPercentage { get; set; }
    }
}
