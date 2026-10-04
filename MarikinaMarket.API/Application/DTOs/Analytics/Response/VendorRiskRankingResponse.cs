using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Analytics.Response
{
    public class VendorRiskRankingResponse
    {
        public int Rank { get; set; }
        public required string BusinessId { get; set; }
        public required string BusinessName { get; set; }
        public int OffenseCount { get; set; }
        public Severity? HighestSeverity { get; set; }
        public int RiskScore { get; set; }
    }
}
