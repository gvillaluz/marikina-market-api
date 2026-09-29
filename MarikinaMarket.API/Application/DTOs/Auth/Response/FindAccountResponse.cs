namespace MarikinaMarket.API.Application.DTOs.Auth.Response
{
    public class FindAccountResponse
    {
        public bool Found { get; set; }
        public required string MaskedEmail { get; set; }
        public required string MaskedPhoneNumber { get; set; }
    }
}