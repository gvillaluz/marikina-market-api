namespace MarikinaMarket.API.Application.DTOs.Enforcers.Response
{
    public class PerformanceSummaryResponse
    {
        public int EnforcerId { get; set; }
        public int TotalInspections { get; set; }
        public double ResolutionRate { get; set; }
        public double WarningRatio { get; set; }
        public double TicketRatio { get; set; }
        public List<MonthInspectionResponse> MonthlyInspections { get; set; } = [];
    }
}