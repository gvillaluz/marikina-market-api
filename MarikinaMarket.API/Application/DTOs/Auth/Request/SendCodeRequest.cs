using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class SendCodeRequest
    {
        [Required(ErrorMessage = "Username must not be empty.")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Channel option must not be empty.")]
        public required string Channel { get; set; }
    }
}