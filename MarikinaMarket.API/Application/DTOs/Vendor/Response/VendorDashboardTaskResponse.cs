using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorDashboardTaskResponse
    {
        public int TicketId { get; set; }
        public string? ControlNumber { get; set; }
        public PenaltyType? PenaltyType { get; set; }
        public TicketStatus? Status { get; set; }
        public int? HoursRequired { get; set; }
        public decimal HoursCompleted { get; set; }
    }
}
