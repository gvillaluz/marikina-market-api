using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class RegistrationInformationRequest
    {
        [Range(1, int.MaxValue)]
        public int VendorRegistrationId { get; set; }

        public uint Version { get; set; }

        [Required(ErrorMessage = "A review reason is required when requesting information.")]
        [StringLength(150, MinimumLength = 1)]
        public required string ReviewReason { get; set; }

        [Required(ErrorMessage = "Review remarks are required when requesting information.")]
        [StringLength(2000, MinimumLength = 1)]
        public required string ReviewRemarks { get; set; }
    }
}
