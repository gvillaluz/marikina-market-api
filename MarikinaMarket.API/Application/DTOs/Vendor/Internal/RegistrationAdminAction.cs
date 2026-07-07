using MarikinaMarket.API.Application.DTOs.Vendor.Request;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class RegistrationAdminAction
    {
        public int VendorRegistrationId { get; set; }
        public RegistrationApprovalRequest? Request { get; set; }
        public int AdminId { get; set; }
    }
}
