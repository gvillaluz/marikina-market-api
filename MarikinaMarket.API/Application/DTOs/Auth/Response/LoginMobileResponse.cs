namespace MarikinaMarket.API.Application.DTOs.Auth.Response
{
    public class LoginMobileResponse
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public required DateTime RefreshTokenExpiration { get; set; }
    }
}
