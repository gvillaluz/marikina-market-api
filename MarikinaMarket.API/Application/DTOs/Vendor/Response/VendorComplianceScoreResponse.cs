namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorComplianceScoreResponse
    {
        public int ComplianceScore { get; set; }
        public required string Standing { get; set; }
        public double ViolationFrequency { get; set; }
        public int TicketsInWindow { get; set; }
        public required string ViolationFrequencyLevel { get; set; }
        public double Recency { get; set; }
        public int? DaysSinceLastTicket { get; set; }
        public double CategorySeverity { get; set; }
        public double? AverageSeverityPoints { get; set; }
        public required string CategorySeverityLevel { get; set; }
        public double PenaltyPaymentHistory { get; set; }
        public int PaymentHistoryTickets { get; set; }
        public DateTime CalculatedAt { get; set; }
    }
}
