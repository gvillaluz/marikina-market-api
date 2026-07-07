namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class TokenRefreshRequest
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
    }
}
