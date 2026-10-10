namespace MarikinaMarket.API.Application.DTOs.Audits.Response
{
    public class AuditLogDetailResponse : AuditLogSummaryResponse
    {
        public string? TargetId { get; set; }
        public required string Details { get; set; }
    }
}
