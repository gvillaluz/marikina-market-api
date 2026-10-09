using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Ordinance.Request
{
    public class SaveOrdinanceRequest
    {
        public required string OrdinanceNumber { get; set; }
        public required string Series { get; set; }
        public required string MarketCode { get; set; }
        public required ViolationCategory Category { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required List<SavePenaltyTierRequest> PenaltyTiers { get; set; }
    }
}
