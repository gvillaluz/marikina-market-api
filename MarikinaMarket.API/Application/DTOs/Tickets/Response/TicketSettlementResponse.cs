using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class TicketSettlementResponse
    {
        public int TicketId { get; set; }
        public PenaltyType PenaltyType { get; set; }
        public List<string> ProofUrls { get; set; } = [];
        public int? CommunityServiceHoursRequired { get; set; }
        public decimal? CommunityServiceHoursCompleted { get; set; }
        public decimal? CommunityServiceHoursRemaining { get; set; }
        public decimal? CommunityServiceCompletionPercentage { get; set; }
        public List<CommunityServiceLogResponse> CommunityServiceLogs { get; set; } = [];
    }

    public class CommunityServiceLogResponse
    {
        public DateTime ServiceDate { get; set; }
        public decimal HoursWorked { get; set; }
        public required string ProofUrl { get; set; }
    }
}
