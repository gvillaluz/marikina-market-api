using MarikinaMarket.API.Application.DTOs.User.Response;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorSelfProfileResponse
    {
        public required UserProfileResponse User { get; set; }
        public required VendorBusinessProfileResponse Vendor { get; set; }
    }
}
