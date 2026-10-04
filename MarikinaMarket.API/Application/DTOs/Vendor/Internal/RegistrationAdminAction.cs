using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Internal
{
    public class RegistrationAdminAction
    {
        public int VendorRegistrationId { get; set; }
        public required RequestStatus RequestStatus { get; set; }
        public string? ReviewReason { get; set; }
        public string? ReviewRemarks { get; set; }
        public required uint Version { get; set; }
        public int AdminId { get; set; }
    }
}
