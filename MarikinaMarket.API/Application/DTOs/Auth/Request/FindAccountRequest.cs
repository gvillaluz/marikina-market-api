using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class FindAccountRequest
    {
        [Required(ErrorMessage = "Username must not be empty.")]
        public required string Username { get; set; }
    }
}