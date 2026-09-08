using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class InspectionSummaryFilters
    {
        [MaxLength(200, ErrorMessage = "Search term must be 200 characters or fewer.")]
        public string? Search { get; set; }

        [EnumDataType(typeof(TicketStatus), ErrorMessage = "Invalid violation type.")]
        public ViolationType? Type { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "MarketSectionId must be a positive number.")]
        public int? MarketSectionId { get; set; }
    }
}