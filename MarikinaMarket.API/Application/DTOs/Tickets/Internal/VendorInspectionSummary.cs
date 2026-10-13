using OrdinanceEntity = MarikinaMarket.API.Domain.Entities.Ordinance;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class VendorInspectionSummary
    {
        public int Id { get; set; }
        public string? ControlNumber { get; set; }
        public ViolationType Type { get; set; }
        public List<OrdinanceEntity> Ordinances { get; set; } = [];
        public DateTime IssuedAt { get; set; }
        public TicketStatus? Status { get; set; }
    }
}
