using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public DateTime DateAndTime { get; set; }
        public required string Activity { get; set; }
        public Module Module { get; set; }
        public int PerformedById { get; set; }
        public User? PerformedBy { get; set; }
        public LogResult Result { get; set; }
    }
}