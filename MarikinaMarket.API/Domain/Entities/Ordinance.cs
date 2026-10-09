using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class Ordinance
    {
        public int Id { get; set; }
        public required string OrdinanceNo { get; set; }
        public required string Series { get; set; }
        public required string MarketCode { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public ViolationCategory Category { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<OrdinancePenaltyTier> PenaltyTiers { get; set; } = [];
        public ICollection<TicketViolation> TicketViolations { get; set; } = [];
    }
}
