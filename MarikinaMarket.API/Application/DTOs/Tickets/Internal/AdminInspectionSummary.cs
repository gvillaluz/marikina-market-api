using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class AdminInspectionSummary
    {
        public int Id { get; set; }
        public int EnforcerId { get; set; }
        public required string EnforcerFirstName { get; set; }
        public required string EnforcerLastName { get; set; }
        public int VendorId { get; set; }
        public required string VendorFirstName { get; set; }
        public required string VendorLastName { get; set; }
        public required string StallNumber { get; set; }
        public required string BusinessName { get; set; }
        public int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public ViolationType Type { get; set; }
        public DateTime IssuedAt { get; set; }
    }
}