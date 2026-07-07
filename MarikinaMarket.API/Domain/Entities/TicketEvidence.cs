namespace MarikinaMarket.API.Domain.Entities
{
    public class TicketEvidence
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        public required string EvidenceUrl { get; set; }
        public DateTime CapturedAt { get; set; }
    }
}
