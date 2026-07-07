using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class FineSummaryResponse
    {
        public decimal TotalPaymentAmount { get; set; }
        public Severity HighestSeverity { get; set; }
        public List<OrdinanceFineBreakdownItem> Breakdown { get; set; } = [];
    }
}
