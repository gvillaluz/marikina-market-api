using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class VendorRegistrationSummary
    {
        public int RegistrationId { get; set; }
        public required string BusinessId { get; set; }
        public required string BusinessName { get; set; }
        public required string VendorName { get; set; }
        public VendorType VendorType { get; set; }
        public required string MarketSectionName { get; set; }
        public string? StallNumber { get; set; }
        public DateTime RequestedAt { get; set; }
        public RequestStatus Status { get; set; }
    }
}
