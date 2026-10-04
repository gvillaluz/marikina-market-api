namespace MarikinaMarket.API.Application.DTOs.Analytics.Internal
{
    public class MarketSectionTrendCount
    {
        public int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public int CurrentWarningCount { get; set; }
        public int CurrentTicketCount { get; set; }
        public int PreviousInspectionCount { get; set; }
    }
}
