using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Username is required.")]
        public required string Username { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "Invalid password length.")]
        public required string Password { get; set; }
    }
}
