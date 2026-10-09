using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Ordinance.Response
{
    public class OrdinanceSummaryResponse
    {
        public int Id { get; set; }
        public required string OrdinanceNo { get; set; }
        public required string Series { get; set; }
        public required string MarketCode { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public ViolationCategory Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public Severity? Severity { get; set; }
        public int PenaltyTierCount { get; set; }
    }
}
