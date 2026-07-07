using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class VendorProfile
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int MarketSectionId { get; set; }
        public MarketSection? MarketSection { get; set; }

        public required string BusinessName { get; set; }
        public required string StallNumber { get; set; }
        public int ComplianceScore { get; set; }
        public DateTime ScoreUpdatedAt { get; set; }
        public required string QrCodeValue { get; set; }

        public VendorStatus Status { get; set; }
        public DateTime RegisteredAt { get; set; }

        public ICollection<Ticket> Tickets { get; set; } = [];
    }
}
