namespace MarikinaMarket.API.Application.DTOs.Auth.Response
{
    public class SendCodeResponse
    {
        public string? Message { get; set; }
        public required int ResendCooldownSeconds { get; set; }
        public required int CodeExpirySeconds { get; set; }
    }
}