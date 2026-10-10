using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int? UserId { get; set; }
        public Role? Role { get; set; }
        public required string Action { get; set; }
        public Module Module { get; set; }
        public string? TargetId { get; set; }
        public LogResult Result { get; set; }
        public required string Details { get; set; }
    }
}
