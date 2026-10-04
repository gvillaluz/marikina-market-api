namespace MarikinaMarket.API.Application.DTOs.Analytics.Response
{
    public class ViolationCategoryDistributionResponse
    {
        public required string Category { get; set; }
        public int TicketCount { get; set; }
        public double Percentage { get; set; }
    }
}
