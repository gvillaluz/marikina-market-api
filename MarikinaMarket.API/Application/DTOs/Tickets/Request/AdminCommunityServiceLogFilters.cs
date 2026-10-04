using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class AdminCommunityServiceLogFilters
    {
        [FromQuery(Name = "status")]
        [EnumDataType(typeof(TicketStatus), ErrorMessage = "Invalid ticket status.")]
        public TicketStatus? Status { get; set; }
    }
}
