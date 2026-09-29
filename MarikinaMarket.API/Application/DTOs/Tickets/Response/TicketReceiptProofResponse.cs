using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class TicketReceiptProofResponse
    {
        public int TicketId { get; set; }
        public PenaltyType PenaltyType { get; set; }
        public List<string> ProofUrls { get; set; } = [];
    }
}
