namespace MarikinaMarket.API.Application.DTOs.Audits.Response
{
    public class AuditLogCountsResponse
    {
        public int RecordedActivities { get; set; }
        public int SuccessfulActions { get; set; }
        public int SecurityEvents { get; set; }
    }
}
