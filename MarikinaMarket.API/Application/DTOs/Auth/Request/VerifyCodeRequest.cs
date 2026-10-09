using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class VerifyCodeRequest
    {
        [Required(ErrorMessage = "Username is required.")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Code is required.")]
        [RegularExpression(@"\A[0-9]{6}\z", ErrorMessage = "Enter a 6-digit code.")]
        public required string Code { get; set; }
    }
}
