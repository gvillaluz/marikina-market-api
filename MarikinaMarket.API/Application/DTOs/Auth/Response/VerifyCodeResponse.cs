namespace MarikinaMarket.API.Application.DTOs.Auth.Response
{
    public class VerifyCodeResponse
    {
        public bool Success { get; set; }
        public required string Message { get; set; }
        public string? ResetToken { get; set; }
    }
}