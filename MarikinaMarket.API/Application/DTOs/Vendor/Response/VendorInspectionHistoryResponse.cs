using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorInspectionHistoryResponse
    {
        public int TicketId { get; set; }
        public string? ControlNumber { get; set; }
        public ViolationType Type { get; set; }
        public DateTime IssuedAt { get; set; }
        public required List<string> OrdinanceNumbers { get; set; }
        public required string EnforcerName { get; set; }
    }
}
