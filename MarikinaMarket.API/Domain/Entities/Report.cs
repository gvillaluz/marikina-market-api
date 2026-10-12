namespace MarikinaMarket.API.Domain.Entities
{
    public class Report
    {
        public int Id { get; set; }
        public required string Type { get; set; }
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public DateTime DateGenerated { get; set; }
        public int GeneratedById { get; set; }
        public required string GeneratedByName { get; set; }
        public int WarningRecords { get; set; }
        public int ResolvedTicketRecords { get; set; }
        public int PendingTicketRecords { get; set; }
        public decimal ResolvedTicketPenalties { get; set; }
        public decimal PendingTicketPenalties { get; set; }
    }
}
