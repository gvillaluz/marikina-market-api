using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Domain.Entities
{
    public class User : IdentityUser<int>
    {
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public required DateOnly DateOfBirth { get; set; }
        public required string HouseNumber { get; set; }
        public required string Street { get; set; }
        public required string Barangay { get; set; }
        public required string City { get; set; }
        public AccountStatus Status { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool MustChangePassword { get; set; } = false;
        public uint Version { get; set; }

        public VendorProfile? VendorProfile { get; set; }
        public ICollection<Ticket> IssuedTickets { get; set; } = [];
        public ICollection<VendorRegistrationRequest> ReviewedVendorRegistrationRequests { get; set; } = [];
        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
        public ICollection<UserDeviceToken> UserDeviceTokens { get; set; } = [];
    }
}
