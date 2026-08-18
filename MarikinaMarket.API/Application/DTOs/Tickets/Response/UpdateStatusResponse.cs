using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class UpdateStatusResponse
    {
        public int TicketId { get; set; }
        public required string ControlNumber { get; set; }
        public TicketStatus Status { get; set; }
        public DateTime UpdatedAt { get; set; }
        public uint Version { get; set; }
    }
}