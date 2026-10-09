namespace MarikinaMarket.API.Application.DTOs.MarketSection.Response
{
    public class MarketSectionResponse
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public bool IsActive { get; set; }
        public int VendorCount { get; set; }
    }
}
