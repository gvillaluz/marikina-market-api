using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class AdminVendorSummaryResponse
    {
        public int VendorId { get; set; }
        public required string BusinessId { get; set; }
        public required string VendorName { get; set; }
        public VendorType Type { get; set; }
        public required string MarketSectionName { get; set; }
        public int ComplianceScore { get; set; }
        public int WarningCount { get; set; }
        public int TicketCount { get; set; }
    }
}