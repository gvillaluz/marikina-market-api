namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorDashboardResponse
    {
        public int TotalWarnings { get; set; }
        public int TotalTickets { get; set; }
        public required VendorComplianceScoreResponse Compliance { get; set; }
        public List<VendorDashboardTaskResponse> Tasks { get; set; } = [];
    }
}
