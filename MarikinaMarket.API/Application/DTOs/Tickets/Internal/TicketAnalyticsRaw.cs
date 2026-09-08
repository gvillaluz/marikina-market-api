namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class TicketAnalyticsRaw
    {
        public int TotalTicketsThisMonth { get; set; }
        public int TotalTicketsLastMonth { get; set; }   
        public decimal PaymentsThisMonth { get; set; }
        public decimal PaymentsLastMonth { get; set; }
        public int ResolvedViolationsThisMonth { get; set; }
        public int HighSeveritiesThisMonth { get; set; }
        public int HighSeveritiesLastMonth { get; set; }
    }
}