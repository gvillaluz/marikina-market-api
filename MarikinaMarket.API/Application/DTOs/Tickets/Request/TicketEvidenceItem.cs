namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class TicketEvidenceItem
    {
        public required string Url { get; set; }
        public DateTime CapturedAt { get; set; }
    }
}
