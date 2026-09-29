namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class CommunityServiceLogResponse
    {
        public DateTime ServiceDate { get; set; }
        public decimal HoursWorked { get; set; }
        public required string ProofUrl { get; set; }
    }
}