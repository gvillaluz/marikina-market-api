using System.Text.Json.Serialization;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class UserProfileResponse
    {
        [JsonPropertyName("userId")]
        public required int UserId { get; set; }
        [JsonPropertyName("username")]
        public required string Username { get; set; }
        [JsonPropertyName("firstName")]
        public required string FirstName { get; set; }
        [JsonPropertyName("middleName")]
        public string? MiddleName { get; set; }
        [JsonPropertyName("lastName")]
        public required string LastName { get; set; }
        [JsonPropertyName("email")]
        public required string Email { get; set; }
        [JsonPropertyName("dateOfBirth")]
        public DateOnly DateOfBirth { get; set; }
        [JsonPropertyName("phoneNumber")]
        public required string PhoneNumber { get; set; }
        [JsonPropertyName("houseNumber")]
        public required string HouseNumber { get; set; }
        [JsonPropertyName("street")]
        public required string Street { get; set; }
        [JsonPropertyName("barangay")]
        public required string Barangay { get; set; }
        [JsonPropertyName("city")]
        public required string City { get; set; }
        [JsonPropertyName("status")]
        public AccountStatus Status { get; set; }
        [JsonPropertyName("role")]
        public Role? Role { get; set; }
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("profileUrl")]
        public string? ProfileUrl { get; set; }
        [JsonPropertyName("mustChangedPassword")]
        public bool MustChangedPassword { get; set; }
    }
}
