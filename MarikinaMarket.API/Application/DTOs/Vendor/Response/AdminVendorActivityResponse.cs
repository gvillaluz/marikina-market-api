using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class AdminVendorActivityResponse
    {
        public int TicketId { get; set; }
        public string? ControlNumber { get; set; }
        public required string BusinessId { get; set; }
        public required string VendorName { get; set; }
        public ViolationType Type { get; set; }
        public DateTime IssuedAt { get; set; }
    }
}