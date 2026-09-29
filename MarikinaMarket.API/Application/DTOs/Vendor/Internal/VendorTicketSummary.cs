using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class VendorTicketSummary
    {
        public int Id { get; set; }
        public required string LastName { get; set; }
        public required string FirstName { get; set; }
        public required string Email { get; set; }
        public int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public required string BusinessName { get; set; }
        public required string BusinessId { get; set; }
        public string? StallNumber { get; set; }
        public VendorType Type { get; set; }
    }
}
