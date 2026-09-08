using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class Notification
    {
        public int Id { get; set; }

        public int EnforcerId { get; set; }
        public User? Enforcer { get; set; }

        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        public TicketStatus Status { get; set; }
        public required string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}