using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class AdminUserResponse
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public Role Role { get; set; }
        public required string Email { get; set; }
        public AccountStatus Status { get; set; }
        public bool MustChangePassword { get; set; }
        public bool EmailSent { get; set; }
        public required string Message { get; set; }
    }
}
