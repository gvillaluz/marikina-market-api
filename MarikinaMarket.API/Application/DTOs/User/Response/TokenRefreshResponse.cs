namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class TokenRefreshResponse
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime RefreshTokenExpiration { get; set; }
    }
}
