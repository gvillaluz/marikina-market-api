using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class VendorInspectionFilters
    {
        [MaxLength(200, ErrorMessage = "Search term must be 200 characters or fewer.")]
        public string? Search { get; set; }

        [EnumDataType(typeof(ViolationType), ErrorMessage = "Invalid violation type.")]
        public ViolationType? Type { get; set; }
    }
}
