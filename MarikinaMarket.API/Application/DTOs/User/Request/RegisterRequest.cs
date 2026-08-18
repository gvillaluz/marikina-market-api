using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "First name is required.")]
        public required string FirstName { get; set; }

        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "Birth date is required.")]
        [DataType(DataType.Date)]
        public DateOnly DateOfBirth { get; set; }

        [Required(ErrorMessage = "Age is required.")]
        [Range(0, 120, ErrorMessage = "Age must be between {1} and {2}")]
        public required int Age { get; set; }

        [Required(ErrorMessage = "Mobile number is required.")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "Mobile number must be a valid PH number (e.g. 09171234567)")]
        public required string MobileNumber { get; set; }

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

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public required string EmailAddress { get; set; }

        [Required(ErrorMessage = "User role is required.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required Role Role { get; set; }
    }
}
