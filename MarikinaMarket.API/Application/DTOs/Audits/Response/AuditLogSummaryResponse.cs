using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Audits.Response
{
    public class AuditLogSummaryResponse
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int? UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Role? Role { get; set; }
        public required string Action { get; set; }
        public Module Module { get; set; }
        public LogResult Result { get; set; }
    }
}
