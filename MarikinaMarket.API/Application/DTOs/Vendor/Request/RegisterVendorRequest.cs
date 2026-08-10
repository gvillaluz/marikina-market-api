using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class RegisterVendorRequest
    {
        [Required(ErrorMessage = "Government ID type is required.")]
        [EnumDataType(typeof(GovernmentIdType), ErrorMessage = "Invalid government ID type.")]
        public GovernmentIdType GovernmentIdType { get; set; }

        [Required(ErrorMessage = "Government ID number is required.")]
        [StringLength(50, MinimumLength = 5, ErrorMessage = "Government ID number must be between 5 and 50 characters.")]
        public required string GovernmentIdNumber { get; set; }

        [Required(ErrorMessage = "Government ID photo is required.")]
        [Url(ErrorMessage = "Government ID photo must be a valid URL.")]
        public required string GovernmentIdPhotoUrl { get; set; }

        [Required(ErrorMessage = "Business document photo is required.")]
        [Url(ErrorMessage = "Business document photo must be a valid URL.")]
        public required string BusinessDocumentPhotoUrl { get; set; }

        [Required(ErrorMessage = "Business name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Business name must be between 2 and 50 characters.")]
        public required string BusinessName { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters.")]
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters.")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public required string Password { get; set; }

        [Required(ErrorMessage = "Stall number is required.")]
        public required string StallNumber { get; set; }

        [Required(ErrorMessage = "Market section is required.")]
        public required int MarketSectionId { get; set; }
    }
}
