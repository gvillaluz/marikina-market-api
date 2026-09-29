using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Username must not be empty.")]
        [StringLength(50, ErrorMessage = "Username must not exceed 50 characters.")]
        public required string Username { get; set; }
        
        [Required(ErrorMessage = "New password must not be empty.")]
        [MinLength(8, ErrorMessage = "New password must be at least 8 characters long.")]
        public required string NewPassword { get; set; }

        [Required(ErrorMessage = "Reset token must not be empty.")]
        public required string ResetToken { get; set; }
    }
}