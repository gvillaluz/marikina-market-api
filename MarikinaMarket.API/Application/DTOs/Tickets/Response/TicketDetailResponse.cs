using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class TicketDetailResponse
    {
        public required int Id { get; init; }
        public required string ControlNumber { get; init; }
        public required int VendorId { get; init; }
        public required string BusinessName { get; init; }
        public required int MarketSectionId { get; init; }
        public required string MarketSectionName { get; init; }
        public required int EnforcerId { get; init; }
        public required TicketType Type { get; init; }
        public required TicketStatus Status { get; init; }
        public required DateTime IssuedAt { get; init; }
        public bool IsOverdue { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
