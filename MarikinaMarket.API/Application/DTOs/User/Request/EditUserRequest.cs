using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class EditUserRequest
    {
        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 100 characters.")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 100 characters.")]
        public required string FirstName { get; set; }

        [StringLength(100, ErrorMessage = "Middle name must not exceed 100 characters.")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Birth date is required.")]
        public DateOnly? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [StringLength(255, ErrorMessage = "Email must not exceed 255 characters.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^09[0-9]{9}$", ErrorMessage = "Phone number must be a valid PH number (e.g. 09171234567).")]
        public required string PhoneNumber { get; set; }

        [Required(ErrorMessage = "House number is required.")]
        [StringLength(50, ErrorMessage = "House number must not exceed 50 characters.")]
        public required string HouseNumber { get; set; }

        [Required(ErrorMessage = "Street is required.")]
        [StringLength(100, ErrorMessage = "Street must not exceed 100 characters.")]
        public required string Street { get; set; }

        [Required(ErrorMessage = "Barangay is required.")]
        [StringLength(100, ErrorMessage = "Barangay must not exceed 100 characters.")]
        public required string Barangay { get; set; }

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100, ErrorMessage = "City must not exceed 100 characters.")]
        public required string City { get; set; }
    }
}
