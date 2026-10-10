namespace MarikinaMarket.API.Application.DTOs.Analytics.Response
{
    public class DashboardSummaryResponse
    {
        public DateTime AsOf { get; set; }
        public required DashboardMetricsResponse Metrics { get; set; }
        public List<DashboardActivityResponse> RecentActivity { get; set; } = [];
        public List<DashboardAttentionItemResponse> AttentionItems { get; set; } = [];
    }

    public class DashboardMetricsResponse
    {
        public int InspectionsToday { get; set; }
        public int OpenTickets { get; set; }
        public int ActiveVendors { get; set; }
        public int PendingRegistrations { get; set; }
    }

    public class DashboardActivityResponse
    {
        public int Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public required string Category { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public string? ActorName { get; set; }
    }

    public class DashboardAttentionItemResponse
    {
        public required string Id { get; set; }
        public required string Type { get; set; }
        public required string Title { get; set; }
        public int Count { get; set; }
    }
}
