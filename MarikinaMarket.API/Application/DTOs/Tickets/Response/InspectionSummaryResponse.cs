using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class InspectionSummaryResponse
    {
        public required int Id { get; init; }
        public required string ControlNumber { get; init; }
        public required int VendorId { get; init; }
        public required string LastName { get; init; }
        public required string FirstName { get; init; }
        public required string BusinessName { get; init; }
        public required int MarketSectionId { get; init; }
        public required string MarketSectionName { get; init; }
        public required string StallNumber { get; init; }
        public required int EnforcerId { get; init; }
        public required ViolationType Type { get; init; }
        public required TicketStatus Status { get; init; }
        public Severity? Severity { get; init; }
        public required List<string> OrdinanceNames { get; init; } = [];
        public required DateTime IssuedAt { get; init; }
        public DateTime? OverdueDate { get; init; }
        public bool IsOverdue { get; init; }
        public DateTime UpdatedAt { get; init; }
        public List<DuplicateOrdinance>? DuplicateOrdinances { get; init; }
        public string? WarningMessageForDuplicates { get; init; }
    }
}
