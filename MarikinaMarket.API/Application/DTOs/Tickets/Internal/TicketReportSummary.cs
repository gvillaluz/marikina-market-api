using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class TicketReportSummary
    {
        public ViolationType Type { get; set; }
        public bool IsResolved { get; set; }
        public int TotalRecords { get; set; }
        public decimal AssessedPenalties { get; set; }
    }
}
