namespace MarikinaMarket.API.Application.DTOs.Analytics.Internal
{
    public class MarketSectionCount
    {
        public int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public int WarningCount { get; set; }
        public int TicketCount { get; set; }
    }
}
