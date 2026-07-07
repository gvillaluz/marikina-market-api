using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class Ticket
    {
        public int Id { get; set; }
        public required string ControlNumber { get; set; }

        public int VendorId { get; set; }
        public VendorProfile? Vendor {  get; set; }

        public int MarketSectionId { get; set; }
        public MarketSection? MarketSection { get; set; }

        public int EnforcerId { get; set; }
        public User? Enforcer { get; set; }

        public TicketType Type { get; set; }
        public TicketStatus Status { get; set; }
        public required string Description { get; set; }
        public decimal TotalPaymentAmount { get; set; }
        public Severity HighestSeverity { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public PenaltyType PenaltyType { get; set; }
        public int? CommunityServiceHours { get; set; }
        public string? ReceiptUrl { get; set; }
        public ViolationCategory PrimaryCategory { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<TicketViolation> TicketViolations { get; set; } = [];
        public ICollection<TicketEvidence> TicketEvidences { get; set; } = [];
    }
}
