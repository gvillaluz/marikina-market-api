using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class RegistrationApprovalRequest
    {
        [Range(1, int.MaxValue)]
        public int VendorRegistrationId { get; set; }

        [Range(typeof(uint), "1", "4294967295", ErrorMessage = "A valid registration version is required.")]
        public uint Version { get; set; }
    }
}
