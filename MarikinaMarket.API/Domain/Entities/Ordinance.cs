using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class Ordinance
    {
        public int Id { get; set; }
        public required string OrdinanceNo { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public ViolationCategory Category { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<OrdinancePenaltyTier> PenaltyTiers { get; set; } = [];
        public ICollection<TicketViolation> TicketViolations { get; set; } = [];
    }
}
