using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "First name is required.")]
        public required string FirstName { get; set; }

        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public required string EmailAddress { get; set; }

        [Required(ErrorMessage = "User role is required.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required Role Role { get; set; }
    }
}
