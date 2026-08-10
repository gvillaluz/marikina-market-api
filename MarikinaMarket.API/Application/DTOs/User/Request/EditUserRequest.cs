using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class EditUserRequest
    {
        [Required(ErrorMessage = "User ID is required.")]
        public required int UserId { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 100 characters.")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 100 characters.")]
        public required string FirstName { get; set; }

        [StringLength(100, ErrorMessage = "Middle name must not exceed 100 characters.")]
        public string? MiddleName { get; set; }
    }
}
