using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class UserSummaryResponse
    {
        public int Id { get; set; }
        public Role? Role { get; set; }
        public required string Username { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public string? MiddleName { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public string? ProfileUrl { get; set; }
        public AccountStatus Status { get; set; }
    }
}
