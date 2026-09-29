using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class VerifyCodeRequest
    {
        [Required(ErrorMessage = "Username must not be empty.")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Code must not be empty.")]
        [MinLength(6, ErrorMessage = "OTP code must be 6 digits.")]
        [MaxLength(6, ErrorMessage = "OTP code must be 6 digits only.")]
        public required string Code { get; set; }
    }
}