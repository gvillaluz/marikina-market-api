namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class TicketAnalyticsResponse
    {
        public int TotalTicketsThisMonth { get; set; }
        public double TicketChangePercentage { get; set; }
        public decimal PendingPaymentsThisMonth { get; set; }
        public double PaymentsChangePercentage { get; set; }
        public int ResolvedViolationsThisMonth { get; set; }
        public double ResolutionRate { get; set; }
        public int HighSeveritiesThisMonth { get; set; }
        public double HighSeveritiesChangePercentage { get; set; }
    }
}