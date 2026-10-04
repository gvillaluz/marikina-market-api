using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class RegisterVendorRequest
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters.")]
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters.")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "Birth date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Mobile number is required.")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "Mobile number must be a valid PH number (e.g. 09171234567)")]
        public required string PhoneNumber { get; set; }

        [Required(ErrorMessage = "House number is required.")]
        [StringLength(50)]
        public required string HouseNumber { get; set; }

        [Required(ErrorMessage = "Street is required.")]
        [StringLength(50)]
        public required string Street { get; set; }

        [Required(ErrorMessage = "Barangay is required.")]
        [StringLength(60)]
        public required string Barangay { get; set; }

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100)]
        public required string City { get; set; }

        [Required(ErrorMessage = "Business ID is required.")]
        [StringLength(20)]
        public required string BusinessId { get; set; }

        [Required(ErrorMessage = "Vendor type is required.")]
        [EnumDataType(typeof(VendorType), ErrorMessage = "Invalid vendor type.")]
        public VendorType VendorType { get; set; }

        [Required(ErrorMessage = "Business name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Business name must be between 2 and 100 characters.")]
        public required string BusinessName { get; set; }

        [Required(ErrorMessage = "Nature of business is required.")]
        [StringLength(100, MinimumLength = 10)]
        public required string NatureOfBusiness { get; set; }

        public string? StallNumber { get; set; }

        [Required(ErrorMessage = "Market section is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "A valid market section is required.")]
        public required int MarketSectionId { get; set; }

        [Required(ErrorMessage = "Government ID type is required.")]
        [EnumDataType(typeof(GovernmentIdType), ErrorMessage = "Invalid government ID type.")]
        public GovernmentIdType? GovernmentIdType { get; set; }

        [FromForm(Name = "government_id_photo")]
        [Required(ErrorMessage = "Government ID photo is required.")]
        public required IFormFile GovernmentIdPhoto { get; set; }

        [FromForm(Name = "business_document_photo")]
        [Required(ErrorMessage = "Business document photo is required.")]
        public required IFormFile BusinessDocumentPhoto { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public required string Email { get; set; }        

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public required string Password { get; set; }

        [Required(ErrorMessage = "Confirm password is required.")]
        [Compare(nameof(Password), ErrorMessage = "Password and confirmation password do not match.")]
        public required string ConfirmPassword { get; set; }
    }
}
