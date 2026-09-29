namespace MarikinaMarket.API.Application.DTOs.Auth.Response
{
    public class ResetPasswordResponse
    {
        public bool Success { get; set; }
        public required string Message { get; set; }
    }
}