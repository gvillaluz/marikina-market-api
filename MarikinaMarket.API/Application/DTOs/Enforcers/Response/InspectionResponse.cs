using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Enforcers.Response 
{
    public class InspectionResponse 
    {
        public int TicketId { get; set; }
        public string? ControlNumber { get; set; }
        public DateTime IssuedAt { get; set; }
        public required string VendorFirstName { get; set; }
        public required string VendorLastName { get; set; }
        public required string StallNumber { get; set; }
        public int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public ViolationType Type { get; set; }
        public TicketStatus? Status { get; set; }
    }
}