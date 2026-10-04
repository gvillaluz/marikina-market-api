using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class VendorInspectionHistoryFilters
    {
        [FromQuery(Name = "ticket_type")]
        [EnumDataType(typeof(ViolationType), ErrorMessage = "Invalid ticket type.")]
        public ViolationType? Type { get; set; }

        [FromQuery(Name = "search")]
        [MaxLength(200, ErrorMessage = "Search term must be 200 characters or fewer.")]
        public string? Search { get; set; }

        [FromQuery(Name = "sort_direction")]
        [RegularExpression("^(asc|desc)$", ErrorMessage = "Sort direction must be 'asc' or 'desc'.")]
        public string SortDirection { get; set; } = "desc";
    }
}
