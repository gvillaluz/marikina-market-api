using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class AdminVendorActivity
    {
        public int TicketId { get; set; }
        public string? ControlNumber { get; set; }
        public required string BusinessId { get; set; }
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public ViolationType Type { get; set; }
        public DateTime IssuedAt { get; set; }
    }
}