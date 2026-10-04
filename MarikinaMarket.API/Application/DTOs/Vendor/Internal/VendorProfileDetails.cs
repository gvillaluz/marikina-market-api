namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class VendorProfileDetails
    {
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public string? Username { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public DateTime AccountCreatedAt { get; set; }
        public DateTime? LastViolationIssuedAt { get; set; }
    }
}
