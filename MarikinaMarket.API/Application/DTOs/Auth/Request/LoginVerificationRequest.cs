using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class LoginVerificationRequest
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, ErrorMessage = "Username is too long.")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be 8–128 characters.")]
        public required string Password { get; set; }

        [Required(ErrorMessage = "Code is required.")]
        [RegularExpression(@"\A[0-9]{6}\z", ErrorMessage = "Enter a 6-digit code.")]
        [StringLength(6, ErrorMessage = "Enter a 6-digit code.")]
        public required string Code { get; set; }
    }
}
