using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class SendCodeRequest
    {
        [Required(ErrorMessage = "Username is required.")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Choose a delivery method.")]
        [RegularExpression(@"(?i)\Aemail\z", ErrorMessage = "Only email is supported.")]
        public required string Channel { get; set; }
    }
}
