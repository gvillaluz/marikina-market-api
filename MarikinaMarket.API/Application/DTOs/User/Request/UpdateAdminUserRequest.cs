using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class UpdateAdminUserRequest : EditUserRequest
    {
        [Required]
        [Range(typeof(uint), "1", "4294967295", ErrorMessage = "A valid account version is required.")]
        public uint? Version { get; set; }
    }
}
