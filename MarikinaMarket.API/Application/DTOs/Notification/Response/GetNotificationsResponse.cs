using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Notification.Response
{
    public class GetNotificationsResponse
    {
        public int Id { get; set; }
        public int EnforcerId { get; set; }
        public int TicketId { get; set; }
        public required string ControlNumber { get; set; }
        public required string TradeName { get; set; }
        public required string MarketSectionName { get; set; }
        public TicketStatus Status { get; set; }
        public required string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}