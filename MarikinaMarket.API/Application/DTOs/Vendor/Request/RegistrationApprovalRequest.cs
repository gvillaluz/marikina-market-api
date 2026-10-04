using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class RegistrationApprovalRequest
    {
        [Range(1, int.MaxValue)]
        public int VendorRegistrationId { get; set; }

        public uint Version { get; set; }
    }
}
