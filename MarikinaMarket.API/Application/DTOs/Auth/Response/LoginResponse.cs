namespace MarikinaMarket.API.Application.DTOs.Auth.Response
{
    public class LoginResponse
    {
        public required string AccessToken { get; set; }
        public required bool MustChangePassword { get; set; }
    }
}