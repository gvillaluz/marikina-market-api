namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class AccountCountsResponse
    {
        public int TotalUsers { get; set; }
        public int TotalStaffUsers { get; set; }
        public int TotalMarketVendorUsers { get; set; }
        public int TotalAdministrators { get; set; }
        public int TotalActiveAccounts { get; set; }
    }
}
