using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Auth.Request
{
    public class AccessTokenRefreshRequest
    {
        [Required(ErrorMessage = "Access token is required.")]
        public required string AccessToken { get; set; }
    }
}
