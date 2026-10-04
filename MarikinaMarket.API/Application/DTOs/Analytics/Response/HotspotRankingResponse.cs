namespace MarikinaMarket.API.Application.DTOs.Analytics.Response
{
    public class HotspotRankingResponse
    {
        public int Rank { get; set; }
        public int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public int WarningCount { get; set; }
        public int TicketCount { get; set; }
        public int TotalInspectionCount { get; set; }
        public int PreviousPeriodCount { get; set; }
        public required string Trend { get; set; }
    }
}
