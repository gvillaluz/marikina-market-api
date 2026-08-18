namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class RegistrationDeclinedResponse
    {
        public int RegistrationId { get; set; }
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required  string LastName { get; set; }
        public required string BusinessName { get; set; }
        public required string RemarksOrReason { get; set; }
        public int AdminId { get; set; }
        public DateTime ReviewedAt { get; set; }
        public uint Version { get; set; }
    }
}