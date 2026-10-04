using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class AdminCommunityServiceLogResponse
    {
        public int TicketId { get; set; }
        public string? ControlNumber { get; set; }
        public required string BusinessId { get; set; }
        public required string VendorName { get; set; }
        public decimal CompletedHours { get; set; }
        public decimal TotalHours { get; set; }
        public DateTime LastUpdated { get; set; }
        public int ProofDocumentCount { get; set; }
        public TicketStatus Status { get; set; }
    }
}
