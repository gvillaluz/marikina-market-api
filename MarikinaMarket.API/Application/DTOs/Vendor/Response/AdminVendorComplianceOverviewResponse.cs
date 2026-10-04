namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class AdminVendorComplianceOverviewResponse
    {
        public int VendorsWithWarningsThisWeek { get; set; }
        public int VendorsWithTicketsThisWeek { get; set; }
        public List<AdminVendorActivityResponse> Activities { get; set; } = [];
    }
}