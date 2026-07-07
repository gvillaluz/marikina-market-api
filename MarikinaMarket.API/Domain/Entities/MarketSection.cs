namespace MarikinaMarket.API.Domain.Entities
{
    public class MarketSection
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<VendorProfile> VendorProfiles { get; set; } = [];
        public ICollection<Ticket> Tickets { get; set; } = [];
    }
}
