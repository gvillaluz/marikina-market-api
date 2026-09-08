namespace MarikinaMarket.API.Application.DTOs.Enforcers.Response
{
    public class TopEnforcerResponse
    {
        public int EnforcerId { get; set; }
        public required string EnforcerName { get; set; }
        public int TotalTickets { get; set; }
    }
}