using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Current password is required.")]
        public required string CurrentPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [MinLength(6, ErrorMessage = "New password must be at least 6 characters long.")]
        [RegularExpression(
            @"^(?=.*\d).+$",
            ErrorMessage = "Password must contain at least one number."
        )]
        public required string NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm your new password.")]
        [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
        public required string ConfirmNewPassword { get; set; }
    }
}
