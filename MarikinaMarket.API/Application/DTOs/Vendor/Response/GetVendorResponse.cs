using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class GetVendorResponse
    {
        public required int VendorId { get; set; }
        public required string Username { get; set; }
        public VendorType Type { get; set; }
        public required string BusinessId { get; set; }
        public string? StallNumber { get; set; }
        public required string TradeName { get; set; }
        public required string LastName { get; set; }
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string Address { get; set; }
        public required int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public required bool CanIssueWarning { get; set; }
        public DateTime? ActiveWarningIssuedAt { get; set; }
    }
}
