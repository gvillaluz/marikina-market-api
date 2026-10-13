using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class VendorInspectionSummaryResponse
    {
        public int TicketId { get; set; }
        public string? ControlNumber { get; set; }
        public ViolationType Type { get; set; }
        public List<string> Violations { get; set; } = [];
        public DateTime IssuedAt { get; set; }
        public DateTime? OverdueDate { get; set; }
        public TicketStatus? TicketStatus { get; set; }
    }
}
