using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class AdminPendingTicketSettlementResponse
    {
        public int TicketId { get; set; }
        public string? ControlNumber { get; set; }
        public required string BusinessId { get; set; }
        public required string VendorName { get; set; }
        public required string MarketSectionName { get; set; }
        public TicketStatus Status { get; set; }
        public PenaltyType? PenaltyType { get; set; }
        public decimal? TotalPaymentAmount { get; set; }
        public int? CommunityServiceHours { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime DueDate { get; set; }
    }
}