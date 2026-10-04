using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class AdminRegisterVendorRequest
    {
        [Required, StringLength(50, MinimumLength = 2)]
        public required string FirstName { get; set; }

        public string? MiddleName { get; set; }

        [Required, StringLength(50, MinimumLength = 2)]
        public required string LastName { get; set; }

        [Required]
        public DateOnly? DateOfBirth { get; set; }

        [Required]
        [RegularExpression(@"^09\d{9}$")]
        public required string PhoneNumber { get; set; }

        [Required, StringLength(50)]
        public required string HouseNumber { get; set; }

        [Required, StringLength(50)]
        public required string Street { get; set; }

        [Required, StringLength(60)]
        public required string Barangay { get; set; }

        [Required, StringLength(100)]
        public required string City { get; set; }

        [Required, StringLength(100)]
        public required string BusinessName { get; set; }

        [Required, StringLength(20)]
        public required string BusinessId { get; set; }

        [Required, StringLength(100, MinimumLength = 2)]
        public required string NatureOfBusiness { get; set; }

        [Range(1, int.MaxValue)]
        public int MarketSectionId { get; set; }

        public string? StallNumber { get; set; }

        [Required]
        [EnumDataType(typeof(VendorType))]
        public VendorType? VendorType { get; set; }

        [Required, EmailAddress]
        public required string Email { get; set; }

        [Required, StringLength(100, MinimumLength = 8)]
        public required string Password { get; set; }

        [Required, Compare(nameof(Password))]
        public required string ConfirmPassword { get; set; }
    }
}
