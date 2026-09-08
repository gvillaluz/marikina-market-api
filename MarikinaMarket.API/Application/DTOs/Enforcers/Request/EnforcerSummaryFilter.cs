using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.Enforcers.Request
{
    public class EnforcerSummaryFilter
    {
        [FromQuery(Name = "search")]
        [MaxLength(200, ErrorMessage = "Search term must be 200 characters or fewer.")]
        public string? Search { get; set; }

        [FromQuery(Name = "status")]
        [EnumDataType(typeof(AccountStatus), ErrorMessage = "Invalid account status.")]
        public AccountStatus? Status { get; set; }

        [FromQuery(Name = "sort_by")]
        [RegularExpression("^(name|tickets|warnings)$", ErrorMessage = "Invalid sort field.")]
        public string? SortBy { get; set; }

        [FromQuery(Name = "sort_direction")]
        [RegularExpression("^(asc|desc)$", ErrorMessage = "Sort direction must be 'asc' or 'desc'.")]
        public string? SortDirection { get; set; }
    }
}