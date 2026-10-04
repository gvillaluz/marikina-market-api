using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class RegistrationDeclineRequest
    {
        [Range(1, int.MaxValue)]
        public int VendorRegistrationId { get; set; }

        public uint Version { get; set; }

        [Required(ErrorMessage = "A review reason is required when declining a registration.")]
        [StringLength(150, MinimumLength = 1)]
        public required string ReviewReason { get; set; }

        [Required(ErrorMessage = "Review remarks are required when declining a registration.")]
        [StringLength(2000, MinimumLength = 1)]
        public required string ReviewRemarks { get; set; }
    }
}
