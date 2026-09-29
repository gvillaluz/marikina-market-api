namespace MarikinaMarket.API.Domain.Entities
{
    public class CommunityServiceLog
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        public DateTime ServiceDate { get; set; }

        public decimal HoursWorked { get; set; }

        public required string ProofUrl { get; set; }

        public int RecordedById { get; set; }
        public User? RecordedBy { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}