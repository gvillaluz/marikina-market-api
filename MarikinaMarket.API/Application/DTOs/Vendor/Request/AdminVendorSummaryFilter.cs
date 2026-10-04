using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Request
{
    public class AdminVendorSummaryFilter
    {
        [FromQuery(Name = "search")]
        [MaxLength(200, ErrorMessage = "Search term must be 200 characters or fewer.")]
        public string? Search { get; set; }

        [FromQuery(Name = "market_section_id")]
        [Range(1, int.MaxValue, ErrorMessage = "Market section must be a positive number.")]
        public int? MarketSectionId { get; set; }

        [FromQuery(Name = "compliance_score")]
        [RegularExpression("^(85-100|70-84|50-69|Below 50)$",
            ErrorMessage = "Compliance score must be 85-100, 70-84, 50-69, or Below 50.")]
        public string? ComplianceScoreRange { get; set; }
    }
}