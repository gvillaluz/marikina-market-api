using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Ordinance.Internal
{
    public class OrdinanceOffenseSummary
    {
        public int OrdinanceId { get; set; }
        public required string OrdinanceNo { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public ViolationCategory Category { get; set; }
        public int OffenseCount { get; set; }
        public List<PenaltyTierSummary> PenaltyTiers { get; set; } = [];
    }
}
