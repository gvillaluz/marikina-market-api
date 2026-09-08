using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class TicketSummaryResponse
    {
        public int Id { get; set; }
        public int EnforcerId { get; set; }
        public int VendorId { get; set; }
        public required string ControlNumber { get; set; }
        public TicketStatus Status { get; set; }
        public required string BusinessName { get; set; }
        public required string StallNumber { get; set; }
        public required string MarketSectionName { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime OverdueDate { get; set; }
    }
}