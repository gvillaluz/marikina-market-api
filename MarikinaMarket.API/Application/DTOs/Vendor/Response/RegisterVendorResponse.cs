namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class RegisterVendorResponse
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string BusinessName { get; set; }
        public required string EmailAddress { get; set; }
        public required string Status {  get; set; }
        public required string Message { get; set; }
    }
}
