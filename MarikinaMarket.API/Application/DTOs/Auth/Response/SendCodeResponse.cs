using System.Text.Json.Serialization;

namespace MarikinaMarket.API.Application.DTOs.Auth.Response
{
    public class SendCodeResponse
    {
        public string? Message { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MaskedEmail { get; set; }
        public required int ResendCooldownSeconds { get; set; }
        public required int CodeExpirySeconds { get; set; }
    }
}
