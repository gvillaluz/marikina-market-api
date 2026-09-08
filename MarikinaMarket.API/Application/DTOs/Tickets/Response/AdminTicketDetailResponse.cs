using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class AdminTicketDetailResponse : TicketDetailResponse
    {
        public required string EnforcerLastName { get; set; }
        public required string EnforcerFirstName { get; set; }
        public List<string> TicketReceipts { get; set; } = [];
        public required uint Version { get; set; }
    }
}