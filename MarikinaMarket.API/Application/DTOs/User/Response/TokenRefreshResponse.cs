namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class TokenRefreshResponse
    {
        public required string NewAccessToken { get; set; }
        public required string RefreshToken { get; set; }
    }
}
