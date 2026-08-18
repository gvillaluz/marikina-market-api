using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class UpdateStatusRequest
    {
        [Required]
        [EnumDataType(typeof(TicketStatus), ErrorMessage = "Invalid ticket status value.")]
        public TicketStatus NewStatus { get; set; }

        [Required(ErrorMessage = "Version is required for concurrency control.")]
        public uint Version { get; set; }
    }
}