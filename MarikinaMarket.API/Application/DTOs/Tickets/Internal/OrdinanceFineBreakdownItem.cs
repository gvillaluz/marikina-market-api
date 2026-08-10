using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class OrdinanceFineBreakdownItem
    {
        public int OrdinanceId { get; set; }
        public required string OrdinanceNo { get; set; }
        public required string OrdinanceCode { get; set; }
        public decimal PaymentAmount { get; set; }
        public Severity Severity { get; set; }
        public int OffenseNumber { get; set; }
        public ViolationCategory Category { get; set; }
        public bool? IsDuplicate { get; set; }
    }
}
