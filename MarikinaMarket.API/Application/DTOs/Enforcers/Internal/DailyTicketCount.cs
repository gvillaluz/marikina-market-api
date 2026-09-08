using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Enforcers.Internal
{
    public class DailyTicketCount
    {
        public DateTime Date { get; set; }
        public int TicketCount { get; set; }
        public int WarningCount { get; set; }
    }
}