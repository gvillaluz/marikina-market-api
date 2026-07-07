namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class RegisterResponse
    {
        public required int UserId { get; set; }
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required string Message { get; set; }
    }
}
