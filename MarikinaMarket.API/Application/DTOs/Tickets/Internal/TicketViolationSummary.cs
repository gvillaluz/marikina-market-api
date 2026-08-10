using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class TicketViolationSummary
    {
        public int TicketViolationId { get; set; }
        public int TicketId { get; set; }
        public int OrdinanceId { get; set; }
        public required string OrdinanceNo { get; set; }
        public required string OrdinanceCode { get; set; }
        public int OffenseCount { get; set; }
        public decimal? PenaltyAmount { get; set; }
    }
}
