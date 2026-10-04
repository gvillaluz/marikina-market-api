using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorProfileResponse
    {
        public required string Name { get; set; }
        public string? Username { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public DateTime AccountCreatedAt { get; set; }
        public Role Role { get; set; }
        public DateTime? LastViolationIssuedAt { get; set; }
    }
}
