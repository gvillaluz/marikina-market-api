using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Enforcers.Response
{
    public class AdminEnforcerSummaryResponse
    {
        public int EnforcerId { get; set; }
        public required string Username { get; set; }
        public required string LastName { get; set; }
        public required string FirstName { get; set; }
        public required string ProfileUrl { get; set; }
        public AccountStatus Status { get; set; }
        public int WarningViolationCount { get; set; }
        public int TicketViolationCount { get; set; }
    }
}