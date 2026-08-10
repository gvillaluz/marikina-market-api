namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class VendorLookupResult
    {
        public required int VendorId;
        public required string StallNumber;
        public required string TradeName;
        public required string LastName;
        public required string FirstName;
        public string? MiddleName;
        public string? Address;
        public required int MarketSectionId;
        public required string MarketSectionName;
    }
}
