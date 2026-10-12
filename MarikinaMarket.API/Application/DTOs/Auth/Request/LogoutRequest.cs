using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class LogoutRequest
    {
        [Required(ErrorMessage = "Device token is required.")]
        [StringLength(500)]
        [RegularExpression(@"\S+", ErrorMessage = "Device token must not contain whitespace.")]
        public required string DeviceToken { get; set; }

        [StringLength(88, MinimumLength = 1)]
        [RegularExpression(@"\S+", ErrorMessage = "Refresh token must not contain whitespace.")]
        public string? RefreshToken { get; set; }
    }
}
