namespace MarikinaMarket.API.Application.DTOs.Enforcers.Response
{
    public class ActivityPanelDataResponse
    {
        public int TotalTicketCount { get; set; }
        public int TotalWarningCount { get; set; }
        public double AverageWarningsPerDay { get; set; }
        public double AverageTicketsPerDay { get; set; }
        public List<TopEnforcerResponse> TopEnforcers { get; set; } = [];
    }
}