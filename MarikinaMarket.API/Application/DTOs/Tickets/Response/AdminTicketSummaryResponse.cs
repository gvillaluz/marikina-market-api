using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class AdminTicketSummaryResponse
    {
        public int Id { get; set; }
        public required string ControlNumber { get; set; }
        public int EnforcerId { get; set; }
        public required string EnforcerFirstName { get; set; }
        public required string EnforcerLastName { get; set; }
        public int VendorId { get; set; }
        public required string VendorFirstName { get; set; }
        public required string VendorLastName { get; set; }
        public required string StallNumber { get; set; }
        public int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public TicketStatus Status { get; set; }
        public Severity Severity { get; set; }
        public PenaltyType PenaltyType { get; set; }
        public decimal TotalPaymentAmount { get; set; }
        public DateTime IssuedAt { get; set; }
    }
}