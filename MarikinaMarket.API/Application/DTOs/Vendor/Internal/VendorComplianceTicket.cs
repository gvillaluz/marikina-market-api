using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class VendorComplianceTicket
    {
        public DateTime IssuedAt { get; set; }
        public Severity? HighestSeverity { get; set; }
        public PenaltyType? PenaltyType { get; set; }
        public decimal? TotalPaymentAmount { get; set; }
        public TicketStatus? Status { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
