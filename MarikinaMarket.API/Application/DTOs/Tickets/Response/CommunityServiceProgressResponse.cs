namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class CommunityServiceProgressResponse
    {
        public int TicketId { get; set; }
        public int HoursRequired { get; set; }
        public decimal HoursCompleted { get; set; }
        public decimal HoursRemaining { get; set; }
        public decimal CompletionPercentage { get; set; }
        public List<CommunityServiceLogResponse> Entries { get; set; } = [];
    }
}
