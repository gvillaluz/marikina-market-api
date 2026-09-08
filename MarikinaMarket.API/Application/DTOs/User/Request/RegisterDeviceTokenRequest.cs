using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class RegisterDeviceTokenRequest
    {
        [Required(ErrorMessage = "Device token is required.")]
        public required string DeviceToken { get; set; }
    }
}