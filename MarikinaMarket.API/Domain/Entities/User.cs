using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Domain.Entities
{
    public class User : IdentityUser<int>
    {
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public AccountStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool MustChangedPassword { get; set; } = false;

        public VendorProfile? VendorProfile { get; set; }
        public ICollection<Ticket> IssuedTickets { get; set; } = [];
        public ICollection<VendorRegistrationRequest> ReviewedVendorRegistrationRequests { get; set; } = [];
        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    }
}
