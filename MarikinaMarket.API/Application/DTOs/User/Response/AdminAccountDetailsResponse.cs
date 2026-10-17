using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class AdminAccountDetailsResponse
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public Role? Role { get; set; }
        public AccountStatus Status { get; set; }
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public required string HouseNumber { get; set; }
        public required string Street { get; set; }
        public required string Barangay { get; set; }
        public required string City { get; set; }
        public uint Version { get; set; }
    }
}
