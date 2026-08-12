namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class WarningCheck
    {
        public required int VendorId { get; set; }
        public required bool CanIssueWarning { get; set; }
        public DateTime? ActiveWarningIssuedAt { get; set; }
    }
}