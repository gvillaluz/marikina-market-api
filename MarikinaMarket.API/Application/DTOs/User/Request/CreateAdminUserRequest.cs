using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class CreateAdminUserRequest
    {
        [Required(ErrorMessage = "Role is required.")]
        [EnumDataType(typeof(Role), ErrorMessage = "Invalid role.")]
        public Role? Role { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, ErrorMessage = "First name is too long.")]
        public required string FirstName { get; set; }

        [StringLength(100, ErrorMessage = "Middle name is too long.")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, ErrorMessage = "Last name is too long.")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "Birth date is required.")]
        public DateOnly? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email.")]
        [StringLength(255, ErrorMessage = "Email is too long.")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [StringLength(11, ErrorMessage = "Enter a valid PH phone number.")]
        [RegularExpression(@"\A09[0-9]{9}\z", ErrorMessage = "Enter a valid PH phone number.")]
        public required string PhoneNumber { get; set; }

        [Required(ErrorMessage = "House number is required.")]
        [StringLength(50, ErrorMessage = "House number is too long.")]
        public required string HouseNumber { get; set; }

        [Required(ErrorMessage = "Street is required.")]
        [StringLength(100, ErrorMessage = "Street is too long.")]
        public required string Street { get; set; }

        [Required(ErrorMessage = "Barangay is required.")]
        [StringLength(100, ErrorMessage = "Barangay is too long.")]
        public required string Barangay { get; set; }

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100, ErrorMessage = "City is too long.")]
        public required string City { get; set; }
    }
}
