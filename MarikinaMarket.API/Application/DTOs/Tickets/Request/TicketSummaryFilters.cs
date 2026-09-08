using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class TicketSummaryFilters
    {
        [FromQuery(Name = "search")]
        [MaxLength(200, ErrorMessage = "Search term must be 200 characters or fewer.")]
        public string? Search { get; set; }

        [FromQuery(Name = "status")]
        [EnumDataType(typeof(TicketStatus), ErrorMessage = "Invalid ticket status.")]
        public TicketStatus? Status { get; set; }

        [FromQuery(Name = "market_section_id")]
        [Range(1, int.MaxValue, ErrorMessage = "Market section must be a positive number.")]
        public int? MarketSectionId { get; set; }
    }
}