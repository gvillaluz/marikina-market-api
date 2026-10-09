using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.User.Request
{
    public class UserSummaryFilter
    {
        [FromQuery(Name = "role")]
        [RegularExpression(@"(?i)\A(All|HeadAdmin|AdminOfficer|MarketEnforcer|MarketVendor|[0-3])\z",
            ErrorMessage = "Invalid account role.")]
        public string? Role { get; set; }

        [FromQuery(Name = "search")]
        [MaxLength(200, ErrorMessage = "Search term must be 200 characters or fewer.")]
        public string? Search { get; set; }
    }
}
