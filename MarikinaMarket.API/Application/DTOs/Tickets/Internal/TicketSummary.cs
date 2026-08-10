using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class TicketSummary
    {
        public required int Id { get; init; }
        public required string ControlNumber { get; init; }
        public required int VendorId { get; init; }
        public required string BusinessName { get; init; }
        public required string MarketSectionName { get; init; }
        public required string StallNumber { get; init; }
        public required int EnforcerId { get; init; }
        public required TicketStatus Status { get; init; }
        public required DateTime IssuedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}