using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class VendorRegistrationRequestFilters
    {
        [FromQuery(Name = "status")]
        public RequestStatus? Status { get; set; }

        [FromQuery(Name = "vendor_type")]
        public VendorType? VendorType { get; set; }
    }
}
