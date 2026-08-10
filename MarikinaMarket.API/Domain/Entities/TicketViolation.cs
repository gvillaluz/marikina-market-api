namespace MarikinaMarket.API.Domain.Entities
{
    public class TicketViolation
    {
        public int Id { get; set; }

        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        public int OrdinanceId { get; set; }
        public Ordinance? Ordinance { get; set; }

        public int OffenseCount { get; set; }

        public decimal? PenaltyAmount { get; set; }
    }
}
